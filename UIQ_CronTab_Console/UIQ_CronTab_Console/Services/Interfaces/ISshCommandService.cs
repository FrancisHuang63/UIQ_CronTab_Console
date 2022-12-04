namespace UIQ_CronTab_Console.Services.Interfaces
{
    public interface ISshCommandService
    {
        public Task<string> RunCommandAsync(string command);
    }
}
