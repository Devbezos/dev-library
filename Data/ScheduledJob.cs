namespace DevClient.Data;

public class ScheduledJob
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int? DayOfWeek { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? LastRun { get; set; }
}
