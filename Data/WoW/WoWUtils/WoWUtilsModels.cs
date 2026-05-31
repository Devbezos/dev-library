using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dev_library.Data.WoW.WoWUtils
{
    /// <summary>
    /// Response from GET /api/droptimizer/fetch?reportId={id}&amp;file=report
    /// Contains the full parsed droptimizer data alongside the raw SimC form text.
    /// </summary>
    public class WoWUtilsFetchResponse
    {
        [JsonProperty("rawFormData")]
        public WoWUtilsRawFormData RawFormData { get; set; }

        // Top-level parsed fields (populated by the wowutils server)
        [JsonProperty("characterName")]
        public string CharacterName { get; set; }

        [JsonProperty("characterClass")]
        public string CharacterClass { get; set; }

        [JsonProperty("characterSpec")]
        public string CharacterSpec { get; set; }

        [JsonProperty("baselineDps")]
        public double BaselineDps { get; set; }

        /// <summary>Sim configuration settings (fightStyle, iterations, buffs, etc.)</summary>
        [JsonProperty("simSettings")]
        public JObject SimSettings { get; set; }

        /// <summary>Per-item DPS gain array with itemId, slot, ilvl, difficulty, etc.</summary>
        [JsonProperty("itemGains")]
        public JArray ItemGains { get; set; }
    }

    public class WoWUtilsRawFormData
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>Raw SimC text used to parse character name, realm, class, spec as a fallback.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    /// <summary>
    /// Response from POST /api/groups/{groupId}/wishlists/{characterSlug}/droptimizer
    /// </summary>
    public class WoWUtilsImportResponse
    {
        [JsonProperty("import")]
        public WoWUtilsImport Import { get; set; }
    }

    public class WoWUtilsImport
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("characterName")]
        public string CharacterName { get; set; }

        [JsonProperty("characterSpec")]
        public string CharacterSpec { get; set; }

        [JsonProperty("reportId")]
        public string ReportId { get; set; }
    }
}
