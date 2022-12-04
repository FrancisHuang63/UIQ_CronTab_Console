namespace UIQ_CronTab_Console.Models
{
    public class MonitoringInfo
    {
        public string Model { get; set; }
        public string Member { get; set; }
        public string Nickname { get; set; }
        public string Account { get; set; }
        public int? Lid { get; set; }
        public string Dtg { get; set; }
        public string Run { get; set; }
        public string Complete_Run_Type { get; set; }
        public string Run_Type { get; set; }
        public string Cron_Mode { get; set; }
        public int? Typhoon_Mode { get; set; }
        public int? Manual { get; set; }
        public int? Start_Flag { get; set; }
        public int? Stage_Flag { get; set; }
        public string Status { get; set; }
        public string Sms_Name { get; set; }
        public DateTime? Sms_Time { get; set; }
        public DateTime? Start_Time { get; set; }
        public DateTime? End_Time { get; set; }
        public DateTime? Pre_Start { get; set; }
        public DateTime? Pre_End { get; set; }
        public DateTime? Run_End { get; set; }
        public string Shell_Name { get; set; }
        public DateTime? Shell_Time { get; set; }
        public string Error_Message { get; set; }

        [Notmapped]
        public string Comment { get; set; }
    }
}