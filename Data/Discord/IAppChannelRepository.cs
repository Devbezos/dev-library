namespace dev_library.Data.Discord
{
    public interface IAppChannelRepository
    {
        void EnsureTable();
        List<AppChannelEntry> Load();
        List<AppChannelEntry> LoadAllIncludingDeleted();
        void Add(string guildName, ulong channelId, string channelName = "");
        void Remove(ulong channelId);
    }
}
