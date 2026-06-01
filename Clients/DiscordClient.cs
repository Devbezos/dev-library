using dev_library.Clients;
using dev_library.Data;
using dev_library.Data.Discord;
using Discord;
using Serilog;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace dev_refined.Clients
{
    public class DiscordClient : IDiscordClient
    {
        public const string TcgMessageHeader = "TCG Stock Monitor";
        public static Func<ulong, string, Task>? SendMessageAsync { get; set; }
        public static Func<ulong, string, Task<ulong>>? SendMessageWithIdAsync { get; set; }
        public static Func<ulong, ulong, string, Task>? EditMessageAsync { get; set; }
        public static Func<ulong, ulong, Embed, Task>? EditEmbedMessageAsync { get; set; }
        public static Func<ulong, Task<ulong?>>? GetLatestBotMessageIdAsync { get; set; }
        public static Func<ulong, Embed, Task>? SendEmbedAsync { get; set; }
        public static Func<ulong, string, Task<ulong>>? CreateApplicationChannelAsync { get; set; }
        public static Func<ulong, Embed, Task<ulong>>? SendEmbedWithIdAsync { get; set; }
        public static Func<ulong, ulong, Task>? PinMessageAsync { get; set; }
        private static readonly ConcurrentDictionary<ulong, ulong[]> _tcgMessageIdsByChannel = new();

        public async Task PostToChannel(ulong channelId, string message)
        {
            Log.Information("DiscordClient.PostToChannel: START");

            if (SendMessageAsync != null)
                await SendMessageAsync(channelId, message);
            else
                Log.Warning("DiscordClient.PostToChannel: SendMessageAsync not wired up. Message: {Message}", message);

            Log.Information("DiscordClient.PostToChannel: END");
        }

        public async Task PostEmbed(ulong channelId, Embed embed)
        {
            Log.Information("DiscordClient.PostEmbed: START");

            if (SendEmbedAsync != null)
                await SendEmbedAsync(channelId, embed);
            else
                Log.Warning("DiscordClient.PostEmbed: SendEmbedAsync not wired up. Title: {Title}", embed.Title);

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
                var applications = await sheetsClient.ReadApplications(guild.ApplicationSheet!);
                var unposted = applications.Where(a => !a.IsPosted).ToList();

                if (unposted.Count == 0) continue;

                foreach (var app in unposted)
                {
                    Log.Information("DiscordClient.CheckNewApplications: Posting application row {Row} from {Contact}", app.RowIndex, app.ContactInfo);
                    var (channelId, channelName, messageIds) = await PostApplication(categoryId, officerChannelId, app);
                    foreach (var msgId in messageIds.Where(id => id != 0))
                        result.Add(new TrackedApplication(msgId, channelId, archiveCategoryId, guild.DenyUserIds, guild.Name, channelName));
                    await sheetsClient.MarkApplicationAsPosted(guild.ApplicationSheet!, app.RowIndex);
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

        public async Task PostWebHook(ulong channelId, List<Search> searchResults)
        {
            Log.Information("DiscordClient.PostWebHook: START");
            var embeds = BuildTcgEmbeds(searchResults);

            try
            {
                if (!_tcgMessageIdsByChannel.TryGetValue(channelId, out var messageIds) && GetLatestBotMessageIdAsync != null)
                {
                    var existing = await GetLatestBotMessageIdAsync(channelId);
                    if (existing.HasValue && existing.Value != 0)
                    {
                        messageIds = [existing.Value];
                        _tcgMessageIdsByChannel[channelId] = messageIds;
                    }
                }

                messageIds ??= [];
                var updatedIds = messageIds.ToList();

                for (var i = 0; i < embeds.Count; i++)
                {
                    var embed = embeds[i];

                    if (i < updatedIds.Count && updatedIds[i] != 0 && EditEmbedMessageAsync != null)
                    {
                        try
                        {
                            await EditEmbedMessageAsync(channelId, updatedIds[i], embed);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "DiscordClient.PostWebHook: edit failed for message {MessageId}, creating a new embed", updatedIds[i]);
                            if (SendEmbedWithIdAsync != null)
                            {
                                var replacementId = await SendEmbedWithIdAsync(channelId, embed);
                                if (replacementId != 0)
                                {
                                    updatedIds[i] = replacementId;
                                    continue;
                                }
                            }
                        }
                    }

                    if (SendEmbedWithIdAsync != null)
                    {
                        var createdId = await SendEmbedWithIdAsync(channelId, embed);
                        if (createdId != 0)
                        {
                            if (i < updatedIds.Count) updatedIds[i] = createdId;
                            else updatedIds.Add(createdId);
                            continue;
                        }
                    }

                    if (SendEmbedAsync != null)
                    {
                        await SendEmbedAsync(channelId, embed);
                    }
                    else
                    {
                        Log.Warning("DiscordClient.PostWebHook: no embed send/edit delegates are wired up");
                    }
                }

                // If we previously needed more pages, replace leftover pages with a notice instead of leaving stale data.
                if (EditEmbedMessageAsync != null && updatedIds.Count > embeds.Count)
                {
                    for (var i = embeds.Count; i < updatedIds.Count; i++)
                    {
                        if (updatedIds[i] == 0) continue;
                        var stale = BuildEmptyContinuationEmbed(i + 1, updatedIds.Count);
                        await EditEmbedMessageAsync(channelId, updatedIds[i], stale);
                    }
                }

                _tcgMessageIdsByChannel[channelId] = updatedIds.ToArray();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DiscordClient.PostWebHook: Failed to send or edit message");
                throw;
            }

            Log.Information("DiscordClient.PostWebHook: END");
        }

        private static List<Embed> BuildTcgEmbeds(List<Search> searchResults)
        {
            var storeBlocks = new List<List<string>>();
            foreach (var storeGroup in searchResults.GroupBy(sr => sr.Store).OrderBy(g => g.Key))
            {
                var block = new List<string> { $"- {storeGroup.Key}" };

                var products = storeGroup
                    .SelectMany(s => s.Products)
                    .GroupBy(p => new { p.Name, p.Price, p.Url })
                    .Select(g => g.First())
                    .ToList();

                foreach (var product in products)
                {
                    block.Add($"  - {product.Url.TrimEnd()}");
                }

                storeBlocks.Add(block);
            }

            var descriptions = BuildEmbedDescriptions(storeBlocks);
            var total = descriptions.Count;

            var embeds = new List<Embed>(total);
            for (var i = 0; i < total; i++)
            {
                var title = total == 1 ? TcgMessageHeader : $"{TcgMessageHeader} ({i + 1}/{total})";
                embeds.Add(new EmbedBuilder()
                    .WithTitle(title)
                    .WithDescription(descriptions[i])
                    .WithFooter($"Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                    .WithColor(Color.Blue)
                    .Build());
            }

            return embeds;
        }

        private static List<string> BuildEmbedDescriptions(List<List<string>> storeBlocks)
        {
            const int max = 3900;

            if (storeBlocks.Count == 0)
                return ["No products found."];

            var chunks = new List<string>();
            var current = new StringBuilder();

            void FlushCurrent()
            {
                if (current.Length == 0) return;
                chunks.Add(current.ToString().TrimEnd());
                current.Clear();
            }

            foreach (var block in storeBlocks)
            {
                var blockText = string.Join("\n", block) + "\n";
                if (blockText.Length <= max)
                {
                    if (current.Length + blockText.Length > max)
                        FlushCurrent();
                    current.Append(blockText);
                    continue;
                }

                // Rare case: one store block alone exceeds max. Split by lines and repeat store header as needed.
                var storeHeader = block.First();
                var lines = block.Skip(1).ToList();
                var local = new StringBuilder();
                local.AppendLine(storeHeader);

                foreach (var line in lines)
                {
                    var safeLine = line.Length > (max - 16) ? line[..(max - 16)] + " ..." : line;
                    if (local.Length + safeLine.Length + 1 > max)
                    {
                        if (current.Length + local.Length > max)
                            FlushCurrent();
                        current.Append(local.ToString());
                        if (current.Length >= max)
                            FlushCurrent();

                        local.Clear();
                        local.AppendLine(storeHeader + " (cont.)");
                    }

                    local.AppendLine(safeLine);
                }

                if (local.Length > 0)
                {
                    if (current.Length + local.Length > max)
                        FlushCurrent();
                    current.Append(local.ToString());
                }
            }

            FlushCurrent();
            return chunks.Count == 0 ? ["No products found."] : chunks;
        }

        private static Embed BuildEmptyContinuationEmbed(int page, int totalPages)
        {
            var title = totalPages <= 1 ? TcgMessageHeader : $"{TcgMessageHeader} ({page}/{totalPages})";
            return new EmbedBuilder()
                .WithTitle(title)
                .WithDescription("No additional pages for this update.")
                .WithFooter($"Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                .WithColor(Color.Blue)
                .Build();
        }
    }
}
