using Microsoft.Extensions.Options;
using UIQ_CronTab_Console.Enums;

namespace UIQ_CronTab_Console.Services
{
    public class MySqlDataBaseNcsLogService : MySqlDataBaseService
    {
        public MySqlDataBaseNcsLogService(IOptions<ConnectoinStringOption> connectoinStringOption) : base(connectoinStringOption, DataBaseEnum.NcsLog)
        {
        }
    }
}