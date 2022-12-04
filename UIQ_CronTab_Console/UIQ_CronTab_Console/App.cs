using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console
{
    public class App
    {
        private readonly ISqlSyncService _sqlSyncService;
        private readonly IParseLogService _parseLogService;
        private readonly IPhaseLogService _phaseLogService;
        private readonly IMakeDailyLogService _makeDailyLogService;

        public App(ISqlSyncService sqlSyncService, IParseLogService parseLogService,
            IPhaseLogService phaseLogService, IMakeDailyLogService makeDailyLogService)
        {
            _sqlSyncService = sqlSyncService;
            _parseLogService = parseLogService;
            _phaseLogService = phaseLogService;
            _makeDailyLogService = makeDailyLogService;
        }

        public async Task Run(string[] args)
        {
            if (args.Any() == false) return;


        }
    }
}