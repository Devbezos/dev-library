using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevClient.Data.WoW.WoWUtils
{
    /// <summary>
    /// Response from GET /api/droptimizer/fetch?reportId={id}&amp;file=report
    /// Contains the full parsed droptimizer data alongside the raw SimC form text.
    /// </summary>
    public class WoWUtilsFetchResponse
    {
        [JsonProperty("rawFormData")]
        public WoWUtilsRawFormData RawFormData { get; set; } = new();

        [JsonProperty("characterName")]
        public string CharacterName { get; set; } = string.Empty;

        [JsonProperty("characterClass")]
        public string CharacterClass { get; set; } = string.Empty;

        [JsonProperty("characterSpec")]
        public string CharacterSpec { get; set; } = string.Empty;

        [JsonProperty("baselineDps")]
        public double BaselineDps { get; set; }

        [JsonProperty("simSettings")]
        public JObject SimSettings { get; set; } = new();

        [JsonProperty("itemGains")]
        public JArray ItemGains { get; set; } = new();
    }

    public class WoWUtilsRawFormData
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class WoWUtilsImportResponse
    {
        [JsonProperty("characterId")]
        public string CharacterId { get; set; } = string.Empty;

        [JsonProperty("profileKey")]
        public string? ProfileKey { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("importedAt")]
        public DateTime ImportedAt { get; set; }

        [JsonProperty("reportUrl")]
        public string ReportUrl { get; set; } = string.Empty;

        [JsonProperty("warnings")]
        public string[] Warnings { get; set; } = [];
    }

    public class WoWUtilsRosterMember
    {
        [JsonProperty("characterId")]
        public string CharacterId { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("realm")]
        public string Realm { get; set; } = string.Empty;

        [JsonProperty("class")]
        public string? Class { get; set; }

        [JsonProperty("spec")]
        public string? Spec { get; set; }

        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("rank")]
        public string? Rank { get; set; }
    }

    public class WoWUtilsGroupListResponse
    {
        [JsonProperty("data")]
        public WoWUtilsGroupSummary[] Data { get; set; } = [];
    }

    public class WoWUtilsGroupSummary
    {
        [JsonProperty("groupId")]
        public string GroupId { get; set; } = string.Empty;
    }

    public class WoWUtilsErrorResponse
    {
        [JsonProperty("error")]
        public WoWUtilsErrorDetail? Error { get; set; }
    }

    public class WoWUtilsErrorDetail
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
    }
}
