using System.Reflection;
using System.Security.Claims;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class LogFileService : ILogFileService
    {
        private string _LogDirectoryPath => $"{RootPath}/logfile";

        public string RootPath
        {
            get
            {
#if DEBUG
                return Directory.GetCurrentDirectory();
#endif
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
        }

        public async Task<string> ReadLogFileAsync(string filePath)
        {
            var checkDirPath = Path.GetDirectoryName(filePath);
            var checkFileName = Path.GetFileName(filePath);
            var checkDir = new DirectoryInfo(checkDirPath);
            //列舉全部檔案再比對檔名
            var checkFile = checkDir.EnumerateFiles().FirstOrDefault(m => m.Name == checkFileName);
            if (checkFile == null || checkFile.Exists == false) return null;

            var logContnet = await System.IO.File.ReadAllTextAsync(Path.Combine(checkDirPath, checkFile.Name));
            return logContnet;
        }

        public async Task WriteDataIntoLogFileAsync(string directoryPath, string fullFilePath, string newData)
        {
            if (System.IO.Directory.Exists(directoryPath) == false)
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            if (System.IO.File.Exists(fullFilePath) == false)
            {
                using (var fs = File.Create(fullFilePath))
                {
                }
            }

            var logContnet = await ReadLogFileAsync(fullFilePath);
            logContnet += logContnet == string.Empty ? newData.TrimStart("\r\n".ToCharArray()) : newData;
            System.IO.File.WriteAllText(fullFilePath, logContnet);
        }

        public async Task WriteUiErrorLogFileAsync(string message)
        {
            var fileFullPath = Path.Combine(_LogDirectoryPath, $"UI_error_{DateTime.Now.ToString("yyyyMMdd")}.log");
            message = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] {message}";
            await WriteDataIntoLogFileAsync(_LogDirectoryPath, fileFullPath, message);
        }

        public async Task WriteUiTransationLogFileAsync(string content)
        {
            var fileFullPath = Path.Combine(_LogDirectoryPath, $"UI_transation_{DateTime.Now.ToString("yyyyMMdd")}.log");
            content = $"\r\n[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff")}] {content}";
            await WriteDataIntoLogFileAsync(_LogDirectoryPath, fileFullPath, content);
        }
    }
}