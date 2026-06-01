using Newtonsoft.Json;

namespace dev_refined.Data
{
    public class Character
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("race")]
        public string Race { get; set; } = string.Empty;

        [JsonProperty("class")]
        public string Class { get; set; } = string.Empty;

        [JsonProperty("active_spec_name")]
        public string ActiveSpecName { get; set; } = string.Empty;

        [JsonProperty("active_spec_role")]
        public string ActiveSpecRole { get; set; } = string.Empty;

        [JsonProperty("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonProperty("faction")]
        public string Faction { get; set; } = string.Empty;

        [JsonProperty("achievement_points")]
        public int AchievementPoints { get; set; }

        [JsonProperty("honorable_kills")]
        public int HonorableKills { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; } = string.Empty;

        [JsonProperty("realm")]
        public string Realm { get; set; } = string.Empty;

        [JsonProperty("last_crawled_at")]
        public DateTime LastCrawledAt { get; set; }

        [JsonProperty("profile_url")]
        public string ProfileUrl { get; set; } = string.Empty;

        [JsonProperty("profile_banner")]
        public string ProfileBanner { get; set; } = string.Empty;
    }

    public class Member
    {
        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("character")]
        public Character Character { get; set; } = new();
    }

    public class WoWAuditGuildResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("faction")]
        public string Faction { get; set; } = string.Empty;

        [JsonProperty("region")]
        public string Region { get; set; } = string.Empty;

        [JsonProperty("realm")]
        public string Realm { get; set; } = string.Empty;

        [JsonProperty("last_crawled_at")]
        public DateTime LastCrawledAt { get; set; }

        [JsonProperty("profile_url")]
        public string ProfileUrl { get; set; } = string.Empty;

        [JsonProperty("members")]
        public List<Member> Members { get; set; } = new();
    }

}
