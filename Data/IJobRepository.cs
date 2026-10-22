namespace DevClient.Data
{
    public interface IJobRepository
    {
        /// <summary>Adds any default job that has no row yet; existing rows keep their settings.</summary>
        void EnsureDefaults();
        List<ScheduledJob> GetAll();
        void Update(ScheduledJob job);
        void MarkRan(string name);
        void ResetLastRun(string name);
        bool ShouldRun(ScheduledJob job, DateTime now);
        bool ShouldRunToday(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz);
        bool ShouldRunThisWeek(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz);
    }
}





