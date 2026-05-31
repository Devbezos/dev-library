using dev_library.Data;
using dev_refined.Data;
using Newtonsoft.Json;
using Serilog;

namespace dev_refined.Clients
{
    public class RaiderIoClient : IRaiderIoClient
    {
        public async Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy)
        {
            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: START");
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(new HttpMethod("GET"),
                $"{Constants.WoW.RaiderIo.Url}/characters/profile?region=us&realm={guildy.Realm}&name={guildy.Name}&fields=mythic_plus_weekly_highest_level_runs,gear");

            request.Headers.TryAddWithoutValidation("accept", "application/json");
            using var httpResponse = await client.SendAsync(request);
            var response = await httpResponse.Content.ReadAsStringAsync();
            var keyResponse = JsonConvert.DeserializeObject<RaiderIoKeyResponse>(response);

            Log.Information("RaiderIoClient.GetWeeklyKeyHistory: END");
            return keyResponse;
        }
    }
}
