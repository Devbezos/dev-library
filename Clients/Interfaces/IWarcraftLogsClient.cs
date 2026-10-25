using DevClient.Data.WoW.WarcraftLogs;

namespace DevClient.Clients
{
    public interface IWarcraftLogsClient
    {
        Task<WarcraftLogsCharacterZoneRankings?> GetCharacterRankings(string? characterName, string? realm, string? region, string characterUrl, int? characterId = null);
        Task<WarcraftLogsReportPage> GetGuildReports(string guildName, string realm, string region, int page = 1, int limit = 25);
        Task<WarcraftLogsReport> GetReportFights(string code);
        /// <summary>The players who took part in a report's boss pulls (one cached query per report).</summary>
        Task<IReadOnlyList<WarcraftLogsReportPlayer>> GetReportPlayers(string code);
        /// <summary>Runs any GraphQL query and returns its <c>data</c> object; GraphQL errors throw <see cref="WarcraftLogsApiException"/>.</summary>
        Task<Newtonsoft.Json.Linq.JToken> QueryAsync(string query, object? variables = null);
    }
}
