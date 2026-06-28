using Newtonsoft.Json;

namespace DevClient.Data.WoW.WoWAudit
{
    public class WoWAuditTrackCharacterRequest
    {
        [JsonProperty("character")]
        public WoWAuditTrackCharacterPayload Character { get; set; } = new();
    }

    public class WoWAuditTrackCharacterPayload
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("realm")]
        public string Realm { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("spec")]
        public string? Spec { get; set; }

        [JsonProperty("rank")]
        public string? Rank { get; set; }

        [JsonProperty("note")]
        public string? Note { get; set; }
    }

    public class WoWAuditUpdateCharacterRequest
    {
        [JsonProperty("character")]
        public WoWAuditUpdateCharacterPayload Character { get; set; } = new();
    }

    public class WoWAuditUpdateCharacterPayload
    {
        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("spec")]
        public string? Spec { get; set; }

        [JsonProperty("rank")]
        public string? Rank { get; set; }

        [JsonProperty("note")]
        public string? Note { get; set; }
    }
}
