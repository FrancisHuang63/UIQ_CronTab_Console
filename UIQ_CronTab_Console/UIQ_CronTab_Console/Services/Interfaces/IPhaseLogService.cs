using UIQ_CronTab_Console.Models.ApiRequest;

namespace UIQ_CronTab_Console.Services.Interfaces
{
    public interface IPhaseLogService
    {
        public Task PhaseLogAsync(PhaseLogRequest request);
    }
}
