using Newtonsoft.Json;

namespace DevClient.Data.WoW.WarcraftLogs
{
    public class WarcraftLogsOAuthResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

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
        public WarcraftLogsEncounter Encounter { get; set; } = new();

        [JsonProperty("rankPercent")]
        public double? RankPercent { get; set; }

        [JsonProperty("totalKills")]
        public int TotalKills { get; set; }

        [JsonProperty("difficulty")]
        public int Difficulty { get; set; }

        [JsonProperty("spec")]
        public string Spec { get; set; } = string.Empty;
    }

    public class WarcraftLogsEncounter
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class WarcraftLogsCharacterZoneRankings
    {
        public string CharacterName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<(string ZoneName, WarcraftLogsZoneRankings Rankings)> ZoneRankings { get; set; } = new();
    }

    /// <summary>Raised when the Warcraft Logs GraphQL API returns an errors array (e.g. unknown guild or report).</summary>
    /// <summary>A player who appears in a report's boss pulls.</summary>
    public record WarcraftLogsReportPlayer(string Name, string? Server, string? Class);

    /// <summary>One player's parses and deaths in one report: the sum and count of their per-encounter parse percents, and their deaths (null when unavailable).</summary>
    public record WarcraftLogsReportPlayerStats(string Name, string? Server, double ParseTotal, int ParseCount, int? Deaths);

    public class WarcraftLogsApiException : Exception
    {
        public WarcraftLogsApiException(string message) : base(message) { }
    }

    public class WarcraftLogsReportPage
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("per_page")]
        public int PerPage { get; set; }

        [JsonProperty("current_page")]
        public int CurrentPage { get; set; }

        [JsonProperty("last_page")]
        public int LastPage { get; set; }

        [JsonProperty("has_more_pages")]
        public bool HasMorePages { get; set; }

        [JsonProperty("data")]
        public List<WarcraftLogsReportSummary> Data { get; set; } = new();
    }

    public class WarcraftLogsReportSummary
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>UNIX timestamp in milliseconds.</summary>
        [JsonProperty("startTime")]
        public double StartTime { get; set; }

        /// <summary>UNIX timestamp in milliseconds.</summary>
        [JsonProperty("endTime")]
        public double EndTime { get; set; }

        [JsonProperty("zone")]
        public WarcraftLogsReportZone? Zone { get; set; }

        [JsonProperty("owner")]
        public WarcraftLogsReportOwner? Owner { get; set; }
    }

    public class WarcraftLogsReportZone
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class WarcraftLogsReportOwner
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class WarcraftLogsReport : WarcraftLogsReportSummary
    {
        [JsonProperty("fights")]
        public List<WarcraftLogsFight> Fights { get; set; } = new();
    }

    public class WarcraftLogsFight
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("encounterID")]
        public int EncounterId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("kill")]
        public bool? Kill { get; set; }

        [JsonProperty("difficulty")]
        public int? Difficulty { get; set; }

        [JsonProperty("size")]
        public int? Size { get; set; }

        /// <summary>Milliseconds relative to the report start.</summary>
        [JsonProperty("startTime")]
        public double StartTime { get; set; }

        /// <summary>Milliseconds relative to the report start.</summary>
        [JsonProperty("endTime")]
        public double EndTime { get; set; }

        [JsonProperty("bossPercentage")]
        public double? BossPercentage { get; set; }

        [JsonProperty("fightPercentage")]
        public double? FightPercentage { get; set; }

        [JsonProperty("lastPhase")]
        public int? LastPhase { get; set; }

        [JsonProperty("lastPhaseIsIntermission")]
        public bool? LastPhaseIsIntermission { get; set; }

        [JsonProperty("averageItemLevel")]
        public double? AverageItemLevel { get; set; }
    }
}





