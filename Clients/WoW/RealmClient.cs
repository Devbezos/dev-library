using DevClient.Data;
using DevClient.Clients;
using Serilog;

namespace DevClient
{
    public class RealmClient
    {
        private readonly IDiscordClient _discordClient;
        private readonly IBattleNetClient _battleNetClient;
        private readonly IServerAvailabilityRepository _repository;

        public RealmClient(IDiscordClient discordClient, IBattleNetClient battleNetClient, IServerAvailabilityRepository repository)
        {
            _discordClient = discordClient;
            _battleNetClient = battleNetClient;
            _repository = repository;
        }

        /// <summary>
        /// Checks each realm a guild monitors once, and when its status changed since the last check posts to every
        /// guild monitoring it. A realm seen for the first time is only recorded; a failed check changes nothing.
        /// </summary>
        public async Task PostServerAvailability()
        {
            var log = Log.ForContext("SourceContext", "RealmClient.PostServerAvailability");
            var realms = _repository.GetMonitored()
                .GroupBy(g => (Region: g.Region!.ToLowerInvariant(), Slug: g.RealmSlug!.ToLowerInvariant()));

            foreach (var realm in realms)
            {
                BattleNetRealmStatus current;
                try
                {
                    current = await _battleNetClient.GetRealmStatus(realm.Key.Region, realm.Key.Slug);
                }
                catch (Exception ex)
                {
                    log.Warning("Could not check {Region}/{Realm}: {Error}", realm.Key.Region, realm.Key.Slug, ex.Message);
                    continue;
                }

                var cached = _repository.GetStatus(realm.Key.Region, realm.Key.Slug);
                if (cached is not null && cached.Status == current.Status)
                    continue;

                _repository.SaveStatus(new RealmStatus(realm.Key.Region, realm.Key.Slug, current.Name, current.Status, DateTime.UtcNow));
                if (cached is null)
                    continue;

                log.Information("{Region}/{Realm} status changed: {Old} -> {New}", realm.Key.Region, realm.Key.Slug, cached.Status, current.Status);
                foreach (var guild in realm)
                {
                    var message = current.Status == "UP"
                        ? $"Servers are back online! maybe? :3{BuildRoleMentions(guild.RoleIds)}"
                        : "Servers have gone offline! maybe? :3";
                    try
                    {
                        await _discordClient.PostToChannel(ulong.Parse(guild.ChannelId!), message);
                    }
                    catch (Exception ex)
                    {
                        log.Warning("Could not post server availability for {Guild}: {Error}", guild.GuildName, ex.Message);
                    }
                }
            }
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
