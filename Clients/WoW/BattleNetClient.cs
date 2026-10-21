using DevClient.Data;
using DevClient.Data.WoW.Realms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;

namespace DevClient.Clients
{
    public class BattleNetClient : IBattleNetClient
    {
        private static readonly TimeSpan RealmIndexLifetime = TimeSpan.FromHours(6);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, IReadOnlyList<BattleNetRealm> Realms)> _realmIndex = new();

        public BattleNetClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;


        public async Task<string> GetOAuthToken()
        {
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(new HttpMethod("POST"), AppSettings.BattleNet.TokenUrl);

            var base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.BattleNet.ClientId}:{AppSettings.BattleNet.ClientSecret}"));
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64authorization}");

            request.Content = new StringContent("grant_type=client_credentials");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

            using var httpResponse = await client.SendAsync(request);
            var response = await httpResponse.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<BlizzardOAuthResponse>(response)!.AccessToken;
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
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(new HttpMethod("GET"), string.Format(AppSettings.BattleNet.ApiUrl + Constants.WoW.BattleNet.ItemNameEndpoint, itemId));
            request.Headers.TryAddWithoutValidation("accept", "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

            using var httpResponse = await client.SendAsync(request);
            var response = await httpResponse.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<JObject>(response)?["name"]?.ToString() ?? string.Empty;
        }

        // The configured API URL is the US one (https://us.api.blizzard.com/data/wow); other regions swap the host.
        private static string ApiUrlFor(string region) =>
            AppSettings.BattleNet.ApiUrl.Replace("://us.", $"://{region}.", StringComparison.OrdinalIgnoreCase);

        private async Task<JObject> GetJson(string token, string url)
        {
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("accept", "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

            using var httpResponse = await client.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();
            return JObject.Parse(await httpResponse.Content.ReadAsStringAsync());
        }
    }
}
