using Newtonsoft.Json;

namespace dev_library.Data.WoW.WarcraftLogs
{
    public class WarcraftLogsOAuthResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class WarcraftLogsZoneRankings
    {
        [JsonProperty("difficulty")]
        public int Difficulty { get; set; }

        [JsonProperty("bestPerformanceAverage")]
        public double? BestPerformanceAverage { get; set; }

        [JsonProperty("rankings")]
        public List<WarcraftLogsBossRanking> Rankings { get; set; } = new();
    }

    public class WarcraftLogsBossRanking
    {
        [JsonProperty("encounter")]
        public WarcraftLogsEncounter Encounter { get; set; }

        [JsonProperty("rankPercent")]
        public double? RankPercent { get; set; }

        [JsonProperty("totalKills")]
        public int TotalKills { get; set; }

        [JsonProperty("difficulty")]
        public int Difficulty { get; set; }

        [JsonProperty("spec")]
        public string Spec { get; set; }
    }

    public class WarcraftLogsEncounter
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class WarcraftLogsCharacterZoneRankings
    {
        public string CharacterName { get; set; }
        public string Url { get; set; }
        public List<(string ZoneName, WarcraftLogsZoneRankings Rankings)> ZoneRankings { get; set; } = new();
    }
}
