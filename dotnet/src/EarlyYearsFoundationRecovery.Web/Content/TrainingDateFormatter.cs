namespace EarlyYearsFoundationRecovery.Web.Content;

public static class TrainingDateFormatter
{
    public static DateOnly ToUkDate(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(timestamp, "Europe/London").DateTime);
}
