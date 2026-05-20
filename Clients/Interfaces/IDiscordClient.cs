using dev_library.Data.Discord;

namespace dev_refined.Clients
{
    public interface IDiscordClient
    {
        Task PostToChannel(ulong channelId, string message);
        Task<(ulong channelId, ulong[] messageIds)> PostApplication(ulong channelId, ulong officerChannelId, GuildApplication app);
    }
}
