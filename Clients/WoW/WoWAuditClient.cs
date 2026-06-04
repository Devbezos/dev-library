using DevClient.Data;
using DevClient.Data.WoW.WoWAudit;
using DevClient.Data.WoW;
using Newtonsoft.Json;
using RestSharp;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace DevClient.Clients
{
    public class WoWAuditClient : IWoWAuditClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WoWAuditClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;
        public async Task<List<WoWAuditCharacter>> GetCharacters(string guild)
        {
            Log.Information("WoWAuditClient.GetCharacters: START {Guild}", guild);
            try
            {
                using var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(new HttpMethod("GET"), $"{Constants.WoW.WoWAudit.Url}/characters");

                request.Headers.TryAddWithoutValidation("accept", "application/json");
                request.Headers.TryAddWithoutValidation("Authorization", AppSettings.Guilds.First(g => g.Name == guild.ToUpper()).Droptimizer?.Token ?? string.Empty);

                using var httpResponse = await client.SendAsync(request);
                var response = await httpResponse.Content.ReadAsStringAsync();

                var guildies = JsonConvert.DeserializeObject<List<WoWAuditCharacter>>(response)!;

                Log.Information("WoWAuditClient.GetCharacters: found {Count}", guildies.Count);
                Log.Information("WoWAuditClient.GetCharacters: END");
                return guildies;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WoWAuditClient.GetCharacters: failed");
                throw;
            }
        }
        public async Task<WoWAuditWishlistResponse> UpdateWishlist(string reportId, string guild)
        {
            var response = "";

            Log.Information("WoWAuditClient.UpdateWishlist: START");
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.Guilds.First(g => g.Name == guild.ToUpper()).Droptimizer?.Token ?? string.Empty);
                var requestBody = new StringContent(JsonConvert.SerializeObject(new WoWAuditWishlistRequest(reportId)), Encoding.UTF8, ContentType.Json);
                using var httpResponse = await client.PostAsync($"{Constants.WoW.WoWAudit.Url}/wishlists", requestBody);
                response = await httpResponse.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WoWAuditWishlistResponse>(response)!;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WoWAuditClient.UpdateWishlist: failed. Body: {Body}", response);
                throw;
            }
            finally
            {
                Log.Information("WoWAuditClient.UpdateWishlist: END");
            }
        }
    }
}




