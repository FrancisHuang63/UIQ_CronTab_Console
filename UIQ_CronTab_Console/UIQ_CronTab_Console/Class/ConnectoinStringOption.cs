using UIQ_CronTab_Console.Enums;

namespace UIQ_CronTab_Console
{
    public class ConnectoinStringOption
    {
        public string NcsUi { get; set; }

        public string NcsLog { get; set; }

        public string GetConnectoinString(DataBaseEnum dataBase)
        {
            switch (dataBase)
            {
                case DataBaseEnum.NcsUi:
                    return NcsUi;
                case DataBaseEnum.NcsLog:
                    return NcsLog;
                default:
                    return NcsUi;
            }
        }
    }
}
