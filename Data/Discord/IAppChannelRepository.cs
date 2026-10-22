namespace DevClient.Data.Discord
{
    public interface IAppChannelRepository
    {
        List<AppChannelEntry> Load();
        List<AppChannelEntry> LoadAllIncludingDeleted();
        void Add(string guildName, ulong channelId, string channelName = "");
        void Remove(ulong channelId);
    }
}





