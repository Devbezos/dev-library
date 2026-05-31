namespace dev_library.Data.Discord
{
    public interface IGuildRepository
    {
        void EnsureTable();
        List<GuildSettingsDto> GetAll();
        List<GuildSettingsDto> GetAllIncludingDeleted();
        GuildSettings[] LoadAsGuildSettings();
        void SyncFromSettings(GuildSettings[] settings);
        void Upsert(GuildSettingsDto dto);
        void Delete(string name);
    }
}
