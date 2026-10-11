using DevClient.Data.WoW.WarcraftLogs;

namespace DevClient.Clients
{
    public interface IWarcraftLogsClient
    {
        Task<WarcraftLogsCharacterZoneRankings?> GetCharacterRankings(string? characterName, string? realm, string? region, string characterUrl, int? characterId = null);
        Task<WarcraftLogsReportPage> GetGuildReports(string guildName, string realm, string region, int page = 1, int limit = 25);
        Task<WarcraftLogsReport> GetReportFights(string code);
    }
}
