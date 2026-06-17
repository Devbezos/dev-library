namespace DevClient.Data.Discord
{
    /// <summary>
    /// Wire-safe DTO for GuildSettings: channel IDs and user IDs are strings
    /// to preserve 64-bit precision across JSON/JavaScript boundaries.
    /// </summary>
    public class GuildSettingsDto
    {
        public string Name { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public Dictionary<string, string> Channels { get; set; } = new();
        public string[] RolesToPing { get; set; } = [];
        public GuildFeatures Features { get; set; } = new();
        public ApplicationReviewSettings Applications { get; set; } = new();
        public RaiderManagementSettings RaiderManagement { get; set; } = new();
        public DroptimizerSettings? Droptimizer { get; set; }
        public GoogleSheetsSettings? GoogleSheet { get; set; }
        public ApplicationSheetSettings? ApplicationSheet { get; set; }
        public string[] DenyUserIds { get; set; } = [];
        public AutoReactionRuleDto[] AutoReactionRules { get; set; } = [];
        public bool IsDeleted { get; set; } = false;

        public static GuildSettingsDto From(GuildSettings g) => new()
        {
            Name     = g.Name ?? string.Empty,
            Nickname = string.Empty,
            Channels = g.Channels?.ToDictionary(k => k.Key, v => v.Value.ToString()) ?? new(),
            RolesToPing = g.RolesToPing ?? [],
            Features = g.Features ?? new(),
            Applications = g.Applications ?? new(),
            RaiderManagement = g.RaiderManagement ?? new(),
            Droptimizer = g.Droptimizer,
            GoogleSheet = g.GoogleSheet,
            ApplicationSheet = g.ApplicationSheet,
            DenyUserIds = g.DenyUserIds?.Select(id => id.ToString()).ToArray() ?? [],
            AutoReactionRules = g.AutoReactionRules?
                .Where(rule => rule.UserId != 0)
                .Select(rule => new AutoReactionRuleDto
                {
                    UserId = rule.UserId.ToString(),
                    EmoteIds = rule.EmoteIds ?? []
                })
                .ToArray() ?? []
        };

        public GuildSettings ToGuildSettings() => new()
        {
            Name = Name,
            Channels = Channels.ToDictionary(k => k.Key, v => ulong.TryParse(v.Value, out var id) ? id : 0UL),
            RolesToPing = RolesToPing,
            Features = Features,
            Applications = Applications ?? new(),
            RaiderManagement = RaiderManagement ?? new(),
            Droptimizer = Droptimizer,
            GoogleSheet = GoogleSheet,
            ApplicationSheet = ApplicationSheet,
            DenyUserIds = DenyUserIds
                .Select(id => ulong.TryParse(id, out var r) ? r : 0UL)
                .Where(id => id != 0)
                .ToArray(),
            AutoReactionRules = AutoReactionRules
                .Select(rule => new AutoReactionRule
                {
                    UserId = ulong.TryParse(rule.UserId, out var userId) ? userId : 0UL,
                    EmoteIds = rule.EmoteIds?
                        .Where(emoteId => !string.IsNullOrWhiteSpace(emoteId))
                        .Select(emoteId => emoteId.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .ToArray() ?? []
                })
                .Where(rule => rule.UserId != 0 && rule.EmoteIds.Length > 0)
                .ToArray()
        };
    }

    public class AutoReactionRuleDto
    {
        public string UserId { get; set; } = string.Empty;
        public string[] EmoteIds { get; set; } = [];
    }
}





