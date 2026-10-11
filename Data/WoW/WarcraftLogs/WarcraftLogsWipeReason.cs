namespace DevClient.Data.WoW.WarcraftLogs
{
    public class WarcraftLogsWipeReason
    {
        public string ReportCode { get; set; } = string.Empty;
        public int FightId { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    public interface IWarcraftLogsWipeReasonRepository
    {
        void EnsureTable();
        List<WarcraftLogsWipeReason> GetForReports(IEnumerable<string> reportCodes);

        /// <summary>Saves the reason for a pull; a blank reason removes it.</summary>
        void Set(string reportCode, int fightId, string guildName, string? reason);
    }
}
