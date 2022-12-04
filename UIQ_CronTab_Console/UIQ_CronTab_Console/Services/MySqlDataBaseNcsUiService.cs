using Microsoft.Extensions.Options;
using UIQ_CronTab_Console;
using UIQ_CronTab_Console.Enums;

namespace UIQ_CronTab_Console.Services
{
    public class MySqlDataBaseNcsUiService : MySqlDataBaseService
    {
        public MySqlDataBaseNcsUiService(IOptions<ConnectoinStringOption> connectoinStringConfigure) : base(connectoinStringConfigure, DataBaseEnum.NcsUi)
        {
        }
    }
}