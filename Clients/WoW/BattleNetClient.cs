using DevClient.Data;
using DevClient.Data.WoW.Realms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Concurrent;
using System.Text;

namespace DevClient.Clients
{
    /// <summary>
    /// Battle.net's game data API. The realm index is kept <see cref="RealmIndexLifetime"/>; a realm's connected-realm link
    /// and item names (static data) a day in <see cref="ApiResponseCache"/>. Realm status is never cached: the server
    /// availability check is there to see it change.
    /// </summary>
    public class BattleNetClient : IBattleNetClient
    {
        private static readonly TimeSpan RealmIndexLifetime = TimeSpan.FromHours(6);
        public static readonly TimeSpan StaticTtl = TimeSpan.FromDays(1);
        private readonly RestClient _http;
        private readonly BattleNetSettings? _settings;
        private readonly ApiResponseCache _cache;
        private readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, IReadOnlyList<BattleNetRealm> Realms)> _realmIndex = new();

        /// <param name="settings">The API passes its own config; without it the bot's static <see cref="AppSettings"/> are used.</param>
        public BattleNetClient(RestClient? http = null, BattleNetSettings? settings = null, ApiResponseCache? cache = null)
        {
            _http = http ?? SharedHttp.Client;
            _settings = settings;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        private BattleNetSettings Settings => _settings ?? AppSettings.BattleNet;

        public async Task<string> GetOAuthToken()
        {
            var base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Settings.ClientId}:{Settings.ClientSecret}"));
            var request = new RestRequest(Settings.TokenUrl, Method.Post)
                .AddHeader("Authorization", $"Basic {base64authorization}")
                .AddParameter("grant_type", "client_credentials");

            var httpResponse = await _http.SendAsync(request);

            return JsonConvert.DeserializeObject<BlizzardOAuthResponse>(httpResponse.Content ?? string.Empty)!.AccessToken;
        }

        public async Task<BattleNetRealmStatus> GetRealmStatus(string region, string realmSlug)
        {
            var token = await GetOAuthToken();
            var realmUrl = $"{ApiUrlFor(region)}/realm/{Uri.EscapeDataString(realmSlug)}?namespace=dynamic-{region}&locale=en_US";
            var realm = JObject.Parse(await _cache.GetOrFetch("bnet:" + realmUrl.ToLowerInvariant(), StaticTtl,
                async () => (await GetJson(token, realmUrl)).ToString(Formatting.None)));
            var connectedRealmUrl = realm["connected_realm"]?["href"]?.ToString();
            if (string.IsNullOrWhiteSpace(connectedRealmUrl))
                throw new InvalidOperationException($"Battle.net returned no connected realm for {region}/{realmSlug}.");

            var connectedRealm = await GetJson(token, connectedRealmUrl + "&locale=en_US");
            var status = connectedRealm["status"]?["type"]?.ToString().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(status))
                throw new InvalidOperationException($"Battle.net returned no status for {region}/{realmSlug}.");

            return new BattleNetRealmStatus(realm["name"]?.ToString() ?? realmSlug, status == "UP" ? "UP" : "DOWN");
        }

        public async Task<IReadOnlyList<BattleNetRealm>> GetRealms(string region)
        {
            if (_realmIndex.TryGetValue(region, out var cached) && DateTime.UtcNow - cached.FetchedAtUtc < RealmIndexLifetime)
                return cached.Realms;

            var token = await GetOAuthToken();
            var index = await GetJson(token, $"{ApiUrlFor(region)}/realm/index?namespace=dynamic-{region}&locale=en_US");
            var realms = (index["realms"] as JArray ?? [])
                .Select(r => new BattleNetRealm(r["slug"]?.ToString() ?? string.Empty, r["name"]?.ToString() ?? string.Empty))
                .Where(r => r.Slug.Length > 0 && r.Name.Length > 0)
                .OrderBy(r => r.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToList();

            _realmIndex[region] = (DateTime.UtcNow, realms);
            return realms;
        }

        public async Task<string> GetItemName(string itemId)
        {
            var url = string.Format(Settings.ApiUrl + Constants.WoW.BattleNet.ItemNameEndpoint, itemId);
            // An empty name (an unknown item or a failed call) isn't kept.
            return await _cache.GetOrFetch("bnet:" + url.ToLowerInvariant(), async () =>
            {
                var token = await GetOAuthToken();
                var request = new RestRequest(url)
                    .AddHeader("accept", "application/json")
                    .AddHeader("Authorization", $"Bearer {token}");

                var httpResponse = await _http.SendAsync(request);
                return httpResponse.IsSuccessStatusCode
                    ? JsonConvert.DeserializeObject<JObject>(httpResponse.Content ?? string.Empty)?["name"]?.ToString() ?? string.Empty
                    : string.Empty;
            }, name => name.Length > 0 ? StaticTtl : TimeSpan.Zero);
        }

        // The configured API URL is the US one (https://us.api.blizzard.com/data/wow); other regions swap the host.
        private string ApiUrlFor(string region) =>
            Settings.ApiUrl.Replace("://us.", $"://{region}.", StringComparison.OrdinalIgnoreCase);

        private async Task<JObject> GetJson(string token, string url)
        {
            var request = new RestRequest(url)
                .AddHeader("accept", "application/json")
                .AddHeader("Authorization", $"Bearer {token}");

            var httpResponse = await _http.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();
            return JObject.Parse(httpResponse.Content ?? string.Empty);
        }
    }
}
