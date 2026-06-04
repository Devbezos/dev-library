using Newtonsoft.Json;

namespace DevClient.Data.WoW.Realms
{
    public class BlizzardOAuthResponse
    {
        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("sub")]
        public string Sub { get; set; } = string.Empty;



    }
}





