namespace DevClient.Data;

public interface IHydrationReminderSettingsRepository
{
    void EnsureTable();
    ulong GetDiscordUserId();
    void SetDiscordUserId(ulong discordUserId);
}
