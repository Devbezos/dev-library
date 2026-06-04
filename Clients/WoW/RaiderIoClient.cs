using DevClient.Data;
using DevClient.Data.WoW;
using Newtonsoft.Json;
using Serilog;

namespace DevClient.Clients
{
    public class RaiderIoClient : IRaiderIoClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RaiderIoClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;
        public async Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy)
        {
            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: START");
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(new HttpMethod("GET"),
                $"{Constants.WoW.RaiderIo.Url}/characters/profile?region=us&realm={guildy.Realm}&name={guildy.Name}&fields=mythic_plus_weekly_highest_level_runs,gear");

            request.Headers.TryAddWithoutValidation("accept", "application/json");
            using var httpResponse = await client.SendAsync(request);
            var response = await httpResponse.Content.ReadAsStringAsync();
            var keyResponse = JsonConvert.DeserializeObject<RaiderIoKeyResponse>(response);

            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: END");
            return keyResponse!;
        }
    }
}





