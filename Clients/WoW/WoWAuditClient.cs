using dev_library.Data;
using dev_library.Data.WoW.WoWAudit;
using dev_refined.Data;
using Newtonsoft.Json;
using RestSharp;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace dev_refined.Clients
{
    public class WoWAuditClient : IWoWAuditClient
    {
        public async Task<List<WoWAuditCharacter>> GetCharacters(string guild)
        {
            Log.Information("WoWAuditClient.GetCharacters: START {Guild}", guild);
            try
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(new HttpMethod("GET"), $"{Constants.WoW.WoWAudit.Url}/characters");

                request.Headers.TryAddWithoutValidation("accept", "application/json");
                request.Headers.TryAddWithoutValidation("Authorization", AppSettings.Guilds.First(g => g.Name == guild.ToUpper()).Droptimizer.Token);

                var response = await client.SendAsync(request).Result.Content.ReadAsStringAsync().ConfigureAwait(false);

                var guildies = JsonConvert.DeserializeObject<List<WoWAuditCharacter>>(response);

                Log.Information($"WoWAuditClient.GetCharacters: found {guildies.Count}");
                Log.Information("WoWAuditClient.GetCharacters: END");
                return guildies;
            }
            catch (Exception)
            {
                Console.WriteLine("WoWAuditClient.GetCharacters: failed");
                throw;
            }
        }
        public async Task<WoWAuditWishlistResponse> UpdateWishlist(string reportId, string guild)
        {
            var response = "";

            Log.Information("WoWAuditClient.UpdateWishlist: START");
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.Guilds.First(g => g.Name == guild.ToUpper()).Droptimizer.Token);
                var requestBody = new StringContent(JsonConvert.SerializeObject(new WoWAuditWishlistRequest(reportId)), Encoding.UTF8, ContentType.Json);
                response = await client.PostAsync($"{Constants.WoW.WoWAudit.Url}/wishlists", requestBody).Result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<WoWAuditWishlistResponse>(response);
            }
            catch (Exception)
            {
                Log.Warning("WoWAuditClient.UpdateWishlist: failed");
                Log.Debug(response);
                throw;
            }
            finally
            {
                Log.Information("WoWAuditClient.UpdateWishlist: END");
            }
        }
    }
}