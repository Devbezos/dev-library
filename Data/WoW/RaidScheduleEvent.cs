namespace DevClient.Data.WoW;

public sealed class RaidScheduleEvent
{
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string? Difficulty { get; set; }
    public string? Status { get; set; }
}