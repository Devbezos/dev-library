using DevClient.Data;
using DevClient.Data.WoW.Realms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Concurrent;
using System.Text;

namespace DevClient.Clients
{
    public class BattleNetClient : IBattleNetClient
    {
        private static readonly TimeSpan RealmIndexLifetime = TimeSpan.FromHours(6);
        private readonly RestClient _http;
        private readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, IReadOnlyList<BattleNetRealm> Realms)> _realmIndex = new();

        public BattleNetClient(RestClient? http = null) => _http = http ?? SharedHttp.Client;


        public async Task<string> GetOAuthToken()
        {
            var base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.BattleNet.ClientId}:{AppSettings.BattleNet.ClientSecret}"));
            var request = new RestRequest(AppSettings.BattleNet.TokenUrl, Method.Post)
                .AddHeader("Authorization", $"Basic {base64authorization}")
                .AddParameter("grant_type", "client_credentials");

            var httpResponse = await _http.SendAsync(request);

            return JsonConvert.DeserializeObject<BlizzardOAuthResponse>(httpResponse.Content ?? string.Empty)!.AccessToken;
        }

        public async Task<BattleNetRealmStatus> GetRealmStatus(string region, string realmSlug)
        {
            var token = await GetOAuthToken();
            var realm = await GetJson(token, $"{ApiUrlFor(region)}/realm/{Uri.EscapeDataString(realmSlug)}?namespace=dynamic-{region}&locale=en_US");
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
            var token = await GetOAuthToken();
            var request = new RestRequest(string.Format(AppSettings.BattleNet.ApiUrl + Constants.WoW.BattleNet.ItemNameEndpoint, itemId))
                .AddHeader("accept", "application/json")
                .AddHeader("Authorization", $"Bearer {token}");

            var httpResponse = await _http.SendAsync(request);

            return JsonConvert.DeserializeObject<JObject>(httpResponse.Content ?? string.Empty)?["name"]?.ToString() ?? string.Empty;
        }

        // The configured API URL is the US one (https://us.api.blizzard.com/data/wow); other regions swap the host.
        private static string ApiUrlFor(string region) =>
            AppSettings.BattleNet.ApiUrl.Replace("://us.", $"://{region}.", StringComparison.OrdinalIgnoreCase);

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
