using Microsoft.Extensions.Configuration;
using UIQ_CronTab_Console.Models.ApiRequest;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class SqlSyncService : ISqlSyncService
    {
        private readonly IDataBaseService _dataBaseNcsUiService;
        private readonly ISshCommandService _sshCommandService;

        private readonly string _HpcCtl;
        private readonly string _SystemName;
        private readonly string _RshAccount;

        public SqlSyncService(IEnumerable<IDataBaseService> dataBaseServices, ISshCommandService sshCommandService, IConfiguration configuration)
        {
            _dataBaseNcsUiService = dataBaseServices.Single(x => x.DataBase == Enums.DataBaseEnum.NcsUi);
            _sshCommandService = sshCommandService;

            _HpcCtl = configuration.GetValue<string>("HpcCTL");
            _SystemName = configuration.GetValue<string>("SystemName");
            _RshAccount = configuration.GetValue<string>("RshAccount");
        }

        public async Task SqlSyncAsync(SqlSyncRequest request)
        {
            var hpcSql = _dataBaseNcsUiService.DataBaseName;
            var account = _dataBaseNcsUiService.DataBaseUid;
            var password = _dataBaseNcsUiService.DataBasePwd;
            var baseDir = $"/{_SystemName}/{_HpcCtl}/web";
            var dateString = DateTime.Now.ToString("yyMMdd");
            var filename = $"{baseDir}/{hpcSql}{dateString}.sql";
            var toHost = string.Empty;
            switch (request.HostName?.Trim())
            {
                case "login13":
                    toHost = "login14";
                    break;

                case "login14":
                    toHost = "login13";
                    break;

                case "datamv13":
                    toHost = "datamv14";
                    break;

                case "datamv14":
                    toHost = "datamv13";
                    break;

                case "h6dm13":
                    toHost = "h6dm14";
                    break;

                case "h6dm14":
                    toHost = "h6dm13";
                    break;

                case "login21":
                    toHost = "login22";
                    break;

                case "login22":
                    toHost = "login21";
                    break;

                case "datamv21":
                    toHost = "datamv22";
                    break;

                case "datamv22":
                    toHost = "datamv21";
                    break;

                case "h6dm21":
                    toHost = "h6dm22";
                    break;

                case "h6dm22":
                    toHost = "h6dm21";
                    break;

                default:
                    break;
            }

            EditDump(filename);

            // copy to TOHOST
            if (!string.IsNullOrEmpty(toHost))
                await _sshCommandService.RunCommandAsync($"sudo -u {_RshAccount} ssh -l {_HpcCtl} {toHost} mysql -u{account} -p{password} --default-character-set=utf8 {hpcSql} < {filename}");
        }

        #region Private Method

        private async void EditDump(string fileName)
        {
            var hpcSql = _dataBaseNcsUiService.DataBaseName;
            var account = _dataBaseNcsUiService.DataBaseUid;
            var password = _dataBaseNcsUiService.DataBasePwd;
            var baseDir = $"/{_SystemName}/{_HpcCtl}/web";

            var options = $"--ignore-table={hpcSql}.history_batch";
            options += $" --ignore-table={hpcSql}.history_batch_model_view";
            options += $" --ignore-table={hpcSql}.history_batch_stage_view";
            options += $" --ignore-table={hpcSql}.archive_view";
            options += $" --ignore-table={hpcSql}.batch_view";
            options += $" --ignore-table={hpcSql}.cron_view";
            options += $" --ignore-table={hpcSql}.model_member_view";
            options += $" --ignore-table={hpcSql}.model_view";
            options += $" --ignore-table={hpcSql}.ouput_view";
            options += $" --ignore-table={hpcSql}.user_view";

            //var sqldump = $"sudo -u {_RshAccount} mysqldump -u{account} -p{password} {hpcSql} {options} > {fileName}";
            var sqldump = $"sudo -u {_RshAccount} mysqldump -u{account} -p{password} {hpcSql} > {fileName}";
            var dump = await _sshCommandService.RunCommandAsync(sqldump);
            /* var dumparr = Regex.Split(dump, "/\n /");
            foreach (var i in dumparr)
            {
                var printStr = string.Empty;
                if (Regex.IsMatch(i, "/^INSERT INTO .+\\(.+$/", RegexOptions.IgnoreCase))
                {
                    var SQLDATA = Regex.Replace(i, "/\\)\\,/", ");\n");
                    var SQLarr = Regex.Split(SQLDATA, "/\n/");
                    foreach (var j in SQLarr)
                    {
                        var prefix = string.Empty;

                        if (Regex.IsMatch(j, "/INSERT INTO/", RegexOptions.IgnoreCase))
                        {
                            var tmparr = Regex.Split(j, "/\\(/");
                            prefix = tmparr[0];
                            printStr = $"{j}\n";
                        }
                        else
                        {
                            printStr = $"{prefix}{j}\n";
                        }
                        _logFileService.WriteDataIntoLogFileAsync(baseDir, fileName, printStr);
                    }
                }
                else
                {
                    printStr = $"{i}\n";
                    _logFileService.WriteDataIntoLogFileAsync(baseDir, fileName, printStr);
                }
            }*/
        }

        #endregion Private Method
    }
}