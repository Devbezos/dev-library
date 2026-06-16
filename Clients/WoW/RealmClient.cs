using DevClient.Data;
using DevClient.Clients;
using Newtonsoft.Json;
using Serilog;

namespace DevClient
{
    public class RealmClient
    {
        private readonly IDiscordClient _discordClient;
        private readonly IBattleNetClient _battleNetClient;

        public RealmClient(IDiscordClient discordClient, IBattleNetClient battleNetClient)
        {
            _discordClient = discordClient;
            _battleNetClient = battleNetClient;
        }

        public async Task<bool> PostServerAvailability()
        {
            var fileLocation = $"{AppSettings.BasePath}/realmcache.json";

            var realmData = await _battleNetClient.GetZuljinData();
            var cachedData = JsonConvert.DeserializeObject<BlizzardRealmResponse>(File.ReadAllText(fileLocation))!;

            File.WriteAllText(fileLocation, JsonConvert.SerializeObject(realmData));

            if (realmData.Status.Name.ToUpper() != cachedData.Status.Name.ToUpper())
            {
                Log.ForContext("SourceContext", "RealmClient.PostServerAvailability")
                   .Information("Server status changed: {Old} -> {New}", cachedData.Status.Name, realmData.Status.Name);

                if (realmData.Status.Name.ToUpper() == "UP")
                {
                    foreach (var guild in AppSettings.Guilds.Where(g => g.Features.ServerAvailability))
                    {
                        var roles = BuildRoleMentions(guild.RolesToPing);
                        await _discordClient.PostToChannel(
                            guild.Channels.GetValueOrDefault("general"),
                            $"Servers are back online! maybe? :3{roles}");
                    }

                    return true;
                }
                else
                {
                    foreach (var guild in AppSettings.Guilds.Where(g => g.Features.ServerAvailability))
                        await _discordClient.PostToChannel(guild.Channels.GetValueOrDefault("general"), "Servers have gone offline! maybe? :3");
                }
            }

            return false;
        }

        private static string BuildRoleMentions(IEnumerable<string>? rolesToPing)
        {
            var mentions = rolesToPing?
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => $"<@&{role.Trim()}>")
                .ToArray();

            return mentions is { Length: > 0 }
                ? $" {string.Join(" ", mentions)}"
                : string.Empty;
        }

    }
}





