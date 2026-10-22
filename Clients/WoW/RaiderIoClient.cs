using DevClient.Data;
using DevClient.Data.WoW;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Serilog;

namespace DevClient.Clients
{
    public class RaiderIoClient : IRaiderIoClient
    {
        private readonly RestClient _http;

        public RaiderIoClient(RestClient? http = null) => _http = http ?? SharedHttp.Client;
        public async Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy)
        {
            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: START");
            var request = new RestRequest(
                $"{Constants.WoW.RaiderIo.Url}/characters/profile?region=us&realm={guildy.Realm}&name={guildy.Name}&fields=mythic_plus_weekly_highest_level_runs,gear")
                .AddHeader("accept", "application/json");

            var httpResponse = await _http.SendAsync(request);
            var keyResponse = JsonConvert.DeserializeObject<RaiderIoKeyResponse>(httpResponse.Content ?? string.Empty);

            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: END");
            return keyResponse!;
        }

        public Task<JToken?> GetCharacterProfile(string region, string realm, string name, string fields) =>
            Get($"{Constants.WoW.RaiderIo.Url}/characters/profile?region={Uri.EscapeDataString(region)}&realm={Uri.EscapeDataString(realm)}&name={Uri.EscapeDataString(name)}&fields={Uri.EscapeDataString(fields)}");

        public Task<JToken?> GetMythicPlusStaticData(int expansionId) =>
            Get($"{Constants.WoW.RaiderIo.Url}/mythic-plus/static-data?expansion_id={expansionId}");

        public Task<JToken?> GetMythicPlusScoreTiers(string? season) =>
            Get($"{Constants.WoW.RaiderIo.Url}/mythic-plus/score-tiers{(string.IsNullOrEmpty(season) ? "" : $"?season={Uri.EscapeDataString(season)}")}");

        // Raider.IO answers 400 (not 404) for an unknown character, realm or expansion, so both mean "not found".
        // Other failures throw without the response body.
        private async Task<JToken?> Get(string url)
        {
            var response = await _http.SendAsync(new RestRequest(url).AddHeader("accept", "application/json"));
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                return null;
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Raider.IO returned {(int)response.StatusCode}.");
            return RawJson.Parse(response.Content ?? string.Empty);
        }
    }
}





