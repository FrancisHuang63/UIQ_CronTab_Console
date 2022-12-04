namespace UIQ_CronTab_Console
{
    public static class DateTimeExtension
    {
        public static DateTime TWNow(this DateTime item)
        {
            return TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
        }
    }
}
