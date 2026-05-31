using dev_library.Data;
using dev_refined.Clients;
using dev_refined.Data;
using Serilog;

namespace dev_refined
{
    public class RefinedClient
    {
        private readonly IWoWAuditClient _wowAuditClient;
        private readonly IRaiderIoClient _raiderIoClient;
        private readonly IDiscordClient _discordClient;

        public RefinedClient(IWoWAuditClient wowAuditClient, IRaiderIoClient raiderIoClient, IDiscordClient discordClient)
        {
            _wowAuditClient = wowAuditClient;
            _raiderIoClient = raiderIoClient;
            _discordClient = discordClient;
        }

        public async Task PostBadPlayers()
        {
            Log.Information("RefinedClient.PostBadPlayers: START");

            foreach (var guild in AppSettings.Guilds.Where(g => g.Features.KeyAudit))
            {
                var badPlayers = new List<BadPlayer>();
                var paddingArray = new int[] { 14, 10, 6 };

                var guildies = await _wowAuditClient.GetCharacters(guild.Name);

                foreach (var guildy in guildies.Where(g => g.Rank.ToUpper() != "RAIDER ALT"))
                {
                    Log.Information($"RefinedClient.PostBadPlayers: Getting key info for {guildy.Name}");

                    var weeklyKeys = await _raiderIoClient.GetWeeklyKeyHistory(guildy);
                    var maxKeyCount = weeklyKeys?.MythicPlusWeeklyHighestLevelRuns.Count(k => k.MythicLevel >= Constants.WoW.MaxKeyLevel) ?? 0;

                    Log.Information($"RefinedClient.PostBadPlayers: {guildy.Name} performed {maxKeyCount} +{Constants.WoW.MaxKeyLevel}s this week");

                    if (maxKeyCount < 8)
                    {
                        badPlayers.Add(new BadPlayer(guildy.Name, 8 - maxKeyCount, decimal.Round(weeklyKeys.Gear.ItemLevel).ToString()));
                    }
                }

                var table = $"Key Audit for +{Constants.WoW.MaxKeyLevel} keys\n```\n";
                table += $"|--------------|----------|------|\n";

                var props = badPlayers[0].GetType().GetProperties();
                for (int i = 0; i < props.Length; i++)
                {
                    table += $"|{props[i].Name.PadBoth(paddingArray[i], ' ')}";
                }

                table += "|";

                foreach (var badPlayer in badPlayers.OrderByDescending(bp => bp.KeysLeft))
                {
                    table += "\n";

                    props = badPlayer.GetType().GetProperties();
                    for (int i = 0; i < props.Length; i++)
                    {
                        table += $"|{props[i].GetValue(badPlayer).ToString().PadBoth(paddingArray[i], ' ')}";
                    }

                    table += "|";
                }

                table += $"\n|--------------|----------|------|\n```";

                await _discordClient.PostToChannel(guild.Channels.GetValueOrDefault("officer"), table);
            }

            Log.Information("RefinedClient.PostBadPlayers: END");
        }
    }
}