namespace UIQ_CronTab_Console.Services.Interfaces
{
    public interface ILogFileService
    {
        string RootPath { get; }

        public Task<string> ReadLogFileAsync(string filePath);

        public Task WriteDataIntoLogFileAsync(string directoryPath, string fullFilePath, string newData);

        public Task WriteUiTransationLogFileAsync(string content);
    }
}
