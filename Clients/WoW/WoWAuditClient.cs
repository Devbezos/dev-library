using DevClient.Data;
using DevClient.Data.WoW;
using DevClient.Data.WoW.WoWAudit;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace DevClient.Clients
{
    public class WoWAuditClient : IWoWAuditClient
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public WoWAuditClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        public async Task<List<WoWAuditCharacter>> GetCharacters(string guild)
        {
            Log.Information("WoWAuditClient.GetCharacters: START {Guild}", guild);
            try
            {
                using var client = CreateAuthorizedClient(guild);
                using var httpResponse = await client.GetAsync($"{Constants.WoW.WoWAudit.Url}/characters");
                var response = await httpResponse.Content.ReadAsStringAsync();
                httpResponse.EnsureSuccessStatusCode();

                var guildies = JsonConvert.DeserializeObject<List<WoWAuditCharacter>>(response) ?? [];

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
            var response = string.Empty;

            Log.Information("WoWAuditClient.UpdateWishlist: START");
            try
            {
                using var client = CreateAuthorizedClient(guild);
                var requestBody = CreateJsonContent(new WoWAuditWishlistRequest(reportId));
                using var httpResponse = await client.PostAsync($"{Constants.WoW.WoWAudit.Url}/wishlists", requestBody);
                response = await httpResponse.Content.ReadAsStringAsync();
                httpResponse.EnsureSuccessStatusCode();
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

        public async Task<WoWAuditCharacter> TrackCharacter(string guild, WoWAuditTrackCharacterRequest request)
        {
            Log.Information("WoWAuditClient.TrackCharacter: START {Guild} {Character}", guild, request.Character.Name);
            using var client = CreateAuthorizedClient(guild);
            using var response = await client.PostAsync(
                $"{Constants.WoW.WoWAudit.Url}/characters",
                CreateJsonContent(request));
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<WoWAuditCharacter>(responseBody) ?? new WoWAuditCharacter();
        }

        public async Task UpdateCharacter(string guild, int characterId, WoWAuditUpdateCharacterRequest request)
        {
            Log.Information("WoWAuditClient.UpdateCharacter: START {Guild} {CharacterId}", guild, characterId);
            using var client = CreateAuthorizedClient(guild);
            using var message = new HttpRequestMessage(HttpMethod.Put, $"{Constants.WoW.WoWAudit.Url}/characters/{characterId}")
            {
                Content = CreateJsonContent(request)
            };
            using var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();
        }

        public async Task UntrackCharacter(string guild, int characterId)
        {
            Log.Information("WoWAuditClient.UntrackCharacter: START {Guild} {CharacterId}", guild, characterId);
            using var client = CreateAuthorizedClient(guild);
            using var response = await client.DeleteAsync($"{Constants.WoW.WoWAudit.Url}/characters/{characterId}");
            response.EnsureSuccessStatusCode();
        }

        private HttpClient CreateAuthorizedClient(string guild)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(GetGuildToken(guild));
            return client;
        }

        private static AuthenticationHeaderValue CreateAuthorizationHeader(string token)
        {
            var trimmed = token.Trim();
            if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticationHeaderValue.Parse(trimmed);

            return new AuthenticationHeaderValue("Bearer", trimmed);
        }

        private static string GetGuildToken(string guild)
        {
            var token = AppSettings.Guilds
                .FirstOrDefault(g => string.Equals(g.Name, guild, StringComparison.OrdinalIgnoreCase))
                ?.Droptimizer?.Token;

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException($"WoWAudit token is not configured for guild '{guild}'");

            return token;
        }

        private static StringContent CreateJsonContent(object payload) =>
            new(JsonConvert.SerializeObject(payload, SerializerSettings), Encoding.UTF8, "application/json");
    }
}
