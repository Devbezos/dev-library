using dev_library.Clients;
using dev_library.Data;
using dev_library.Data.Discord;
using Discord;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace dev_refined.Clients
{
    public class DiscordClient : IDiscordClient
    {
        public static Func<ulong, string, Task>? SendMessageAsync { get; set; }
        public static Func<ulong, Embed, Task>? SendEmbedAsync { get; set; }
        public static Func<ulong, string, Task<ulong>>? CreateApplicationChannelAsync { get; set; }
        public static Func<ulong, Embed, Task<ulong>>? SendEmbedWithIdAsync { get; set; }
        public static Func<ulong, ulong, Task>? PinMessageAsync { get; set; }

        public async Task PostToChannel(ulong channelId, string message)
        {
            Log.Information("DiscordClient.PostToChannel: START");

            if (SendMessageAsync != null)
                await SendMessageAsync(channelId, message);
            else
                Console.WriteLine($"[WARN] DiscordClient.SendMessageAsync not wired up. Message: {message}");

            Log.Information("DiscordClient.PostToChannel: END");
        }

        public async Task PostEmbed(ulong channelId, Embed embed)
        {
            Log.Information("DiscordClient.PostEmbed: START");

            if (SendEmbedAsync != null)
                await SendEmbedAsync(channelId, embed);
            else
                Console.WriteLine($"[WARN] DiscordClient.SendEmbedAsync not wired up. Embed: {embed.Title}");

            Log.Information("DiscordClient.PostEmbed: END");
        }

        public async Task<(ulong channelId, string channelName, ulong[] messageIds)> PostApplication(ulong channelId, ulong officerChannelId, GuildApplication app)
        {
            Log.Information("DiscordClient.PostApplication: START");

            if (CreateApplicationChannelAsync == null || SendEmbedWithIdAsync == null)
            {
                Log.Warning("DiscordClient.PostApplication: delegates not wired up");
                return (0, string.Empty, Array.Empty<ulong>());
            }

            var channelName = SanitizeChannelName(app.ContactInfo);
            var threadId = await CreateApplicationChannelAsync(channelId, channelName);
            var msgId = await SendEmbedWithIdAsync(threadId, app.ToEmbed());
            if (msgId != 0 && PinMessageAsync != null)
                await PinMessageAsync(threadId, msgId);

            var officerMessage = app.ToOfficerMessage();
            if (officerMessage != null)
                await PostToChannel(officerChannelId, officerMessage);

            Log.Information("DiscordClient.PostApplication: END");
            return (threadId, channelName, new[] { msgId });
        }

        private static string SanitizeChannelName(string contactInfo)
        {
            // Find last hyphen, take one word to the left and everything to the right
            var lastHyphen = contactInfo.LastIndexOf('-');
            string name;
            if (lastHyphen > 0)
            {
                var beforeHyphen = contactInfo[..lastHyphen].TrimEnd();
                var lastSpace = beforeHyphen.LastIndexOf(' ');
                var wordBefore = lastSpace >= 0 ? beforeHyphen[(lastSpace + 1)..] : beforeHyphen;
                var afterHyphen = contactInfo[(lastHyphen + 1)..].Trim();
                name = $"{wordBefore}-{afterHyphen}";
            }
            else
            {
                name = contactInfo.Trim();
            }
            name = Regex.Replace(name.ToLower(), @"[^a-z0-9\-]", "");
            if (name.Length > 100) name = name[..100];
            return string.IsNullOrEmpty(name) ? "application" : name;
        }

        private static List<string> SplitMessage(string message, int maxLength)
        {
            var chunks = new List<string>();
            var lines = message.Split('\n');
            var current = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (current.Length + line.Length + 1 > maxLength)
                {
                    chunks.Add(current.ToString().TrimEnd());
                    current.Clear();
                }
                current.AppendLine(line);
            }

            if (current.Length > 0)
                chunks.Add(current.ToString().TrimEnd());

            return chunks;
        }

        public async Task<List<TrackedApplication>> CheckNewApplications(GoogleSheetsClient sheetsClient)
        {
            Log.Debug("DiscordClient.CheckNewApplications: START");
            var result = new List<TrackedApplication>();
            var guildsWithApps = AppSettings.Guilds.Where(g =>
                g.ApplicationSheet != null &&
                g.Channels?.ContainsKey("applicationsCategory") == true &&
                g.Channels?.ContainsKey("applicationsOfficer") == true);

            foreach (var guild in guildsWithApps)
            {
                var categoryId = guild.Channels["applicationsCategory"];
                var officerChannelId = guild.Channels["applicationsOfficer"];
                var archiveCategoryId = guild.Channels.GetValueOrDefault("applicationsArchiveCategory");
                var applications = await sheetsClient.ReadApplications(guild.ApplicationSheet);
                var unposted = applications.Where(a => !a.IsPosted).ToList();

                if (unposted.Count == 0) continue;

                foreach (var app in unposted)
                {
                    Log.Information("DiscordClient.CheckNewApplications: Posting application row {Row} from {Contact}", app.RowIndex, app.ContactInfo);
                    var (channelId, channelName, messageIds) = await PostApplication(categoryId, officerChannelId, app);
                    foreach (var msgId in messageIds.Where(id => id != 0))
                        result.Add(new TrackedApplication(msgId, channelId, archiveCategoryId, guild.DenyUserIds, guild.Name, channelName));
                    await sheetsClient.MarkApplicationAsPosted(guild.ApplicationSheet, app.RowIndex);
                }
            }

            Log.Debug("DiscordClient.CheckNewApplications: END");
            return result;
        }

        public async Task SendDroptimizerReminders(DateTime now)
        {
            Log.Debug("DiscordClient.SendDroptimizerReminders: START");
            foreach (var guild in AppSettings.Guilds.Where(g => g.Features.DroptimizerReminder && Helpers.IsGuildActive(g, now)))
            {
                var roles = guild.RolesToPing?.Length > 0
                    ? string.Join(" ", guild.RolesToPing.Select(r => $"<@&{r}>")) + " "
                    : "";
                var channelId = guild.Channels?.GetValueOrDefault("droptimizer") ?? 0;
                if (channelId != 0)
                    await PostToChannel(channelId, $"{roles}Make sure to post droptimizers or you're not getting loot");
            }
            Log.Debug("DiscordClient.SendDroptimizerReminders: END");
        }

        public async Task PostWebHook(List<Search> searchResults)
        {
            Log.Information("DiscordClient.PostWebHook: START");
            foreach (var storeGroup in searchResults.GroupBy(sr => sr.Store))
            {
                var webHookValue = $"- {storeGroup.Key}\n";

                foreach (var itemInStock in storeGroup)
                {
                    webHookValue += $"  - {itemInStock.Keyword}\n";

                    foreach (var product in itemInStock.Products)
                    {
                        var productInfo = $"New Item Now In Stock: {product.Name}, Price: {product.Price}";
                        Console.WriteLine($"Program.PostResults: {productInfo}");
                        webHookValue += $"      - {product.Url}";
                    }
                }

                try
                {
                    if (SendMessageAsync != null)
                        await SendMessageAsync(AppSettings.Guilds.First(g => g.Name == "POKEMON").Channels.GetValueOrDefault("general"), webHookValue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }

            Log.Information("DiscordClient.PostWebHook: END");
        }
    }
}
