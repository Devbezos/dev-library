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

        // Top-level parsed fields (populated by the wowutils server)
        [JsonProperty("characterName")]
        public string CharacterName { get; set; } = string.Empty;

        [JsonProperty("characterClass")]
        public string CharacterClass { get; set; } = string.Empty;

        [JsonProperty("characterSpec")]
        public string CharacterSpec { get; set; } = string.Empty;

        [JsonProperty("baselineDps")]
        public double BaselineDps { get; set; }

        /// <summary>Sim configuration settings (fightStyle, iterations, buffs, etc.)</summary>
        [JsonProperty("simSettings")]
        public JObject SimSettings { get; set; } = new();

        /// <summary>Per-item DPS gain array with itemId, slot, ilvl, difficulty, etc.</summary>
        [JsonProperty("itemGains")]
        public JArray ItemGains { get; set; } = new();
    }

    public class WoWUtilsRawFormData
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>Raw SimC text used to parse character name, realm, class, spec as a fallback.</summary>
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
}





