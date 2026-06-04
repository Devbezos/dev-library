using Newtonsoft.Json;

namespace DevClient.Data.WoW.WoWAudit
{
    public class WoWAuditWishlistRequest
    {
        public WoWAuditWishlistRequest(string reportId)
        {
            ReportId = reportId;
            ConfigurationName = "Single Target";
        }

        [JsonProperty("report_id")]
        public string ReportId { get; set; }

        [JsonProperty("configuration_name")]
        public string ConfigurationName { get; set; }
    }
}





