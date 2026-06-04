using MySqlConnector;

namespace DevClient.Data
{
    public interface ITcgChannelSettingsRepository
    {
        void EnsureTable();
        ulong GetChannelId(string game);
        ulong[] GetNotificationUserIds(string game);
        Dictionary<string, ulong> GetAll();
        void SetChannelId(string game, ulong channelId);
        void SetNotificationUserIds(string game, IEnumerable<ulong> userIds);
        void EnsureDefault(string game, ulong channelId);
    }
}



