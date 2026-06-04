using Newtonsoft.Json;

namespace DevClient.Data.WoW.WoWAudit
{
    public class WoWAuditWishlistResponse
    {
        [JsonProperty("created")]
        public string Created { get; set; } = string.Empty;
        [JsonProperty("base")]
        public string[] Base { get; set; } = [];
    }
}





