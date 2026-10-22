using DevClient.Data;
using DevClient.Data.WoW;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Serilog;

namespace DevClient.Clients
{
    /// <summary>
    /// Raider.IO's public API. Answers are cached (<see cref="ApiResponseCache"/>): character profiles for
    /// <see cref="ProfileTtl"/>, static data and score tiers for <see cref="StaticTtl"/>, "not found" for
    /// <see cref="MissingTtl"/>, failures never.
    /// </summary>
    public class RaiderIoClient : IRaiderIoClient
    {
        public static readonly TimeSpan ProfileTtl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan StaticTtl = TimeSpan.FromHours(6);
        public static readonly TimeSpan MissingTtl = TimeSpan.FromMinutes(2);

        private readonly RestClient _http;
        private readonly ApiResponseCache _cache;

        public RaiderIoClient(RestClient? http = null, ApiResponseCache? cache = null)
        {
            _http = http ?? SharedHttp.Client;
            _cache = cache ?? ApiResponseCache.Shared;
        }

        public async Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy)
        {
            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: START");
            var url = $"{Constants.WoW.RaiderIo.Url}/characters/profile?region=us&realm={guildy.Realm}&name={guildy.Name}&fields=mythic_plus_weekly_highest_level_runs,gear";

            // Only a success is kept; anything else is read as before and fetched again next time.
            var (content, _) = await _cache.GetOrFetch("rio:" + url, async () =>
            {
                var httpResponse = await _http.SendAsync(new RestRequest(url).AddHeader("accept", "application/json"));
                return (Content: httpResponse.Content, Ok: httpResponse.IsSuccessStatusCode);
            }, answer => answer.Ok ? ProfileTtl : TimeSpan.Zero);
            var keyResponse = JsonConvert.DeserializeObject<RaiderIoKeyResponse>(content ?? string.Empty);

            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: END");
            return keyResponse!;
        }

        public Task<JToken?> GetCharacterProfile(string region, string realm, string name, string fields) =>
            Get($"{Constants.WoW.RaiderIo.Url}/characters/profile?region={Uri.EscapeDataString(region)}&realm={Uri.EscapeDataString(realm)}&name={Uri.EscapeDataString(name)}&fields={Uri.EscapeDataString(fields)}", ProfileTtl);

        public Task<JToken?> GetMythicPlusStaticData(int expansionId) =>
            Get($"{Constants.WoW.RaiderIo.Url}/mythic-plus/static-data?expansion_id={expansionId}", StaticTtl);

        public Task<JToken?> GetMythicPlusScoreTiers(string? season) =>
            Get($"{Constants.WoW.RaiderIo.Url}/mythic-plus/score-tiers{(string.IsNullOrEmpty(season) ? "" : $"?season={Uri.EscapeDataString(season)}")}", StaticTtl);

        // Raider.IO answers 400 (not 404) for an unknown character, realm or expansion, so both mean "not found".
        // Other failures throw without the response body. The body is what's cached, so each caller parses its own copy.
        private async Task<JToken?> Get(string url, TimeSpan ttl)
        {
            var body = await _cache.GetOrFetch("rio:" + url.ToLowerInvariant(), ttl, async () =>
            {
                var response = await _http.SendAsync(new RestRequest(url).AddHeader("accept", "application/json"));
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    return null;
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Raider.IO returned {(int)response.StatusCode}.");
                return response.Content ?? string.Empty;
            }, MissingTtl);
            return body is null ? null : RawJson.Parse(body);
        }
    }
}





