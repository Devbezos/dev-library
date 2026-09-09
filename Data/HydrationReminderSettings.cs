namespace DevClient.Data;

// Days is indexed like DateTime.DayOfWeek: 0 = Sunday ... 6 = Saturday.
// Start/EndMinuteOfDay are minutes since midnight (e.g. 9:00 AM = 540), local time.
public sealed record HydrationReminderSchedule(
    ulong DiscordUserId,
    bool Enabled,
    bool[] Days,
    int StartMinuteOfDay,
    int EndMinuteOfDay,
    int IntervalMinutes,
    DateTime? LastSentAtUtc);

public interface IHydrationReminderSettingsRepository
{
    void EnsureTable();
    HydrationReminderSchedule Get();
    void Update(ulong discordUserId, bool enabled, bool[] days, int startMinuteOfDay, int endMinuteOfDay, int intervalMinutes);
    void MarkSent(DateTime sentAtUtc);
}
