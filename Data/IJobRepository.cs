namespace dev_library.Data
{
    public interface IJobRepository
    {
        void EnsureTable();
        List<ScheduledJob> GetAll();
        void Update(ScheduledJob job);
        void MarkRan(string name);
        void ResetLastRun(string name);
        bool ShouldRun(ScheduledJob job, DateTime now);
        bool ShouldRunToday(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz);
        bool ShouldRunThisWeek(ScheduledJob job, DateTime nowEastern, TimeZoneInfo tz);
    }
}
