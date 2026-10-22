namespace DevClient.Data.Discord
{
    public enum GuildUpdateResult { NotFound, Updated, NameConflict }

    public interface IGuildRepository
    {
        List<GuildSettingsDto> GetAll();
        List<GuildSettingsDto> GetAllIncludingDeleted();
        GuildSettings[] LoadAsGuildSettings();
        void SyncFromSettings(GuildSettings[] settings);
        void Upsert(GuildSettingsDto dto);
        GuildUpdateResult Update(string originalName, GuildSettingsDto dto);
        void Delete(string name);
    }
}





