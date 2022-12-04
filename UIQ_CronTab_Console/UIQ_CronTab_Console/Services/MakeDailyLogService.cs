using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class MakeDailyLogService : IMakeDailyLogService
    {
        private readonly ISshCommandService _sshCommandService;
        private readonly ILogFileService _logFileService;

        private readonly string _systemName;
        private readonly string _uiPath;
        private readonly string _hpcCtl;
        private readonly string _hpcDailyLog;

        public MakeDailyLogService(ISshCommandService sshCommandService, ILogFileService logFileService, IConfiguration configuration)
        {
            _sshCommandService = sshCommandService;
            _logFileService = logFileService;

            _systemName = configuration.GetValue<string>("SystemName");
            _uiPath = configuration.GetValue<string>("UiPath");
            _hpcCtl = configuration.GetValue<string>("HpcCTL");
            _hpcDailyLog = configuration.GetValue<string>("HpcDailyLog");
        }

        public async Task MakeDailyLogAsync()
        {
            var allAccPath = (await _sshCommandService.RunCommandAsync($"sh {_uiPath}shell/log_path.sh")).TrimEnd();
            var allAccArr = Regex.Split(allAccPath, "/ /");

            var nowDate = DateTime.Now.TWNow().AddSeconds(-86400).ToString("MMdd");
            var fileDerictoryPath = $"/{_systemName}/{_hpcCtl}/{_hpcDailyLog}";
            var filePath = $"{fileDerictoryPath}/log.{nowDate}";

            foreach (var accPath in allAccArr)
            {
                var pathArr = Regex.Split(accPath, "%/%");
                var model = pathArr[3];
                var member = pathArr[4];
                if (Regex.IsMatch(member, "/E[0-9][0-9]/")) continue;

                var logFilePath = $"{accPath}/log/job.{nowDate}";
                var printString = string.Empty;
                if (File.Exists(logFilePath) == false)
                {
                    printString = $"There is no log file {logFilePath}\n";
                    printString += "---------------------------------------------------------\n";

                    await _logFileService.WriteDataIntoLogFileAsync(fileDerictoryPath, filePath, printString);
                    continue;
                }

                var logContent = (await _logFileService.ReadLogFileAsync(logFilePath)).Split("\n");
                var flg = 0;
                for (var i = 0; i < logContent.Count(); i++)
                {
                    var j = i + 1;
                    if (Regex.IsMatch(logContent[i], $"/{model}/") && !Regex.IsMatch(logContent[i], "/^\\[/"))
                    {
                        flg = flg == 1 ? flg : 1;

                        var header = logContent[i].TrimEnd();
                        var startTime = Regex.Split(logContent[j], "/ /").FirstOrDefault();

                        printString = $"{(flg == 1 ? "\n" : string.Empty)}{header.PadLeft(29, '-')} {startTime.PadLeft(10, '-')}";
                        printString += $"{(flg == 1 ? "\n" : string.Empty)}------------------------------------------------------------\n";
                        await _logFileService.WriteDataIntoLogFileAsync(fileDerictoryPath, filePath, printString);

                        flg = flg == 1 ? 0 : flg;

                        continue;
                    }

                    if (Regex.IsMatch(logContent[i], "/finish|fail|cancelled/i"))
                    {
                        flg = 0;
                        var finishArr = Regex.Split(logContent[i].TrimEnd(), "/ /");
                        var finishTime = finishArr.FirstOrDefault();
                        var finishResult = finishArr.LastOrDefault();

                        printString = $" {finishTime} {finishResult}\n";
                        printString += $"{(flg == 1 ? "\n" : string.Empty)}------------------------------------------------------------\n";
                        await _logFileService.WriteDataIntoLogFileAsync(fileDerictoryPath, filePath, printString);
                        continue;
                    }

                    printString += $"{(flg == 1 ? "\n" : string.Empty)}------------------------------------------------------------\n";
                    await _logFileService.WriteDataIntoLogFileAsync(fileDerictoryPath, filePath, printString);
                    flg = flg == 1 ? 0 : flg;
                }
            }
        }
    }
}