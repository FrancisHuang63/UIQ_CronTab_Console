using System.Diagnostics;

namespace UIQ_CronTab_Console.Services.Interfaces
{
    public class SshCommandService : ISshCommandService
    {
        public async Task<string> RunCommandAsync(string command)
        {
            var output = string.Empty;
            if (string.IsNullOrWhiteSpace(command)) return string.Empty;
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \" {command} \"",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                return output;
                
            }
            catch
            {
                return command;
            }
        }
    }
}