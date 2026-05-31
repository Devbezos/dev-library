using dev_library.Data.Discord;
using Discord;

namespace dev_refined.Clients
{
    public interface IDiscordClient
    {
        Task PostToChannel(ulong channelId, string message);
        Task PostEmbed(ulong channelId, Embed embed);
        Task<(ulong channelId, string channelName, ulong[] messageIds)> PostApplication(ulong channelId, ulong officerChannelId, GuildApplication app);
    }
}
