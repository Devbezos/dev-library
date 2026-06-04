using DevClient.Clients;
using DevClient.Data;
using DevClient.Data.Discord;
using Discord;

namespace DevClient.Clients
{
    public interface IDiscordClient
    {
        Task PostToChannel(ulong channelId, string message);
        Task PostEmbed(ulong channelId, Embed embed);
        Task<(ulong channelId, string channelName, ulong[] messageIds)> PostApplication(ulong channelId, ulong officerChannelId, GuildApplication app);
        Task<List<TrackedApplication>> CheckNewApplications(GoogleSheetsClient sheetsClient);
        Task SendDroptimizerReminders(DateTime now);
        Task PostWebHook(ulong channelId, List<Search> searchResults);
        Task SendDirectMessage(ulong userId, string message);
    }
}





