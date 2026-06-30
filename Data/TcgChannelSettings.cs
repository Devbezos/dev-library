namespace DevClient.Data
{
    public sealed record TcgPackRule(string Keyword, int PackCount);

    public interface ITcgChannelSettingsRepository
    {
        void EnsureTable();
        ulong GetChannelId(string game);
        ulong[] GetChannelIds(string game);
        ulong[] GetNotificationUserIds(string game);
        IReadOnlyList<TcgPackRule> GetPackRules(string game);
        Dictionary<string, ulong> GetAll();
        Dictionary<string, ulong[]> GetAllChannelIds();
        void SetChannelId(string game, ulong channelId);
        void SetChannelIds(string game, IEnumerable<ulong> channelIds);
        void SetNotificationUserIds(string game, IEnumerable<ulong> userIds);
        void SetPackRules(string game, IEnumerable<TcgPackRule> rules);
        void EnsureDefault(string game, ulong channelId);
    }
}
