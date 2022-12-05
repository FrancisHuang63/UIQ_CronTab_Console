using UIQ_CronTab_Console.Enums;
using UIQ_CronTab_Console.Models.ApiRequest;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console
{
    public class App
    {
        private readonly ISqlSyncService _sqlSyncService;
        private readonly IParseLogService _parseLogService;
        private readonly IPhaseLogService _phaseLogService;
        private readonly IMakeDailyLogService _makeDailyLogService;
        private readonly ILogFileService _logFileService;

        public App(ISqlSyncService sqlSyncService, IParseLogService parseLogService,
            IPhaseLogService phaseLogService, IMakeDailyLogService makeDailyLogService, ILogFileService logFileService)
        {
            _sqlSyncService = sqlSyncService;
            _parseLogService = parseLogService;
            _phaseLogService = phaseLogService;
            _makeDailyLogService = makeDailyLogService;
            _logFileService = logFileService;
        }

        public async Task RunAsync(string[] args)
        {
            if (args.Any() == false) return;

            _logFileService.WriteUiTransationLogFileAsync($"args : {System.Text.Json.JsonSerializer.Serialize(args)}");
            try
            {
                switch (args.FirstOrDefault())
                {
                    case nameof(ServiceTypeEnum.SqlSync):
                        await _sqlSyncService.SqlSyncAsync(new SqlSyncRequest { HostName = (args.ElementAtOrDefault(1) ?? string.Empty) });
                        return;

                    case nameof(ServiceTypeEnum.ParseLog): await _parseLogService.ParseLogAsync(); return;

                    case nameof(ServiceTypeEnum.PhaseLog):
                        var fileDate = DateTime.TryParse(args.ElementAtOrDefault(1), out var tmpFileDate) ? (DateTime?)tmpFileDate : null;
                        await _phaseLogService.PhaseLogAsync(new PhaseLogRequest { FileDate = fileDate });
                        return;

                    case nameof(ServiceTypeEnum.MakeDailyLog): await _makeDailyLogService.MakeDailyLogAsync(); return;

                    default: return;
                }
            }
            catch (Exception ex)
            {
                _logFileService.WriteUiErrorLogFileAsync(ex.ToString());
                throw;
            }
        }
    }
}