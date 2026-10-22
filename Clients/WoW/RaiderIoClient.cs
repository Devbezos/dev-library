using DevClient.Data;
using DevClient.Data.WoW;
using Newtonsoft.Json;
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
    }
}





