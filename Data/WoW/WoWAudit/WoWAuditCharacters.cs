using Newtonsoft.Json;

namespace DevClient.Data.WoW
{
    public class WoWAuditCharacter
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("realm")]
        public string Realm { get; set; } = string.Empty;

        [JsonProperty("class")]
        public string Class { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty;

        [JsonProperty("rank")]
        public string Rank { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("note")]
        public string Note { get; set; } = string.Empty;

        [JsonProperty("blizzard_id")]
        public int BlizzardId { get; set; }

        [JsonProperty("tracking_since")]
        public DateTime TrackingSince { get; set; }
    }
}





