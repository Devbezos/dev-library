using Microsoft.Extensions.Configuration;

namespace DevClient.Data
{
    public static class AppSettings
    {
        public static bool DryRun { get; set; }
        public static DiscordSettings Discord { get; set; } = new();
        public static BattleNetSettings BattleNet { get; set; } = new();
        public static GuildSettings[] Guilds { get; set; } = [];
        public static SoundboardSettings Soundboard { get; set; } = new();
        public static string BasePath { get; set; } = $"{Path.GetPathRoot(AppContext.BaseDirectory)}Code";
        public static string ApiSettingsPath { get; set; } = string.Empty;

        public static MySqlSettings MySql { get; set; } = new();
        public static WarcraftLogsSettings? WarcraftLogs { get; set; }
        public static GoogleSheetsCredentialsSettings GoogleSheets { get; set; } = new();
        public static GoogleHealthUserSettings[] GoogleHealth { get; set; } = Array.Empty<GoogleHealthUserSettings>();
        public static FitnessWeightSheetSettings FitnessWeightSheet { get; set; } = new();

        public static void Initialize()
        {
            var localConfig = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            ApiSettingsPath = ResolveApiSettingsPath(localConfig.GetValue<string>("apiSettingsPath"));

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory);

            if (!string.IsNullOrWhiteSpace(ApiSettingsPath))
                builder.AddJsonFile(ApiSettingsPath, optional: true, reloadOnChange: false);

            var config = builder
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            DryRun = config.GetValue<bool>("dryRun");
            ApiSettingsPath = config.GetValue<string>("apiSettingsPath") ?? ApiSettingsPath;
            Discord = config.GetSection("discord").Get<DiscordSettings>() ?? new();
            BattleNet = config.GetSection("battleNet").Get<BattleNetSettings>() ?? new();
            Guilds = config.GetSection("guilds").Get<GuildSettings[]>() ?? Array.Empty<GuildSettings>();
            Soundboard = config.GetSection("soundboard").Get<SoundboardSettings>() ?? new SoundboardSettings();
            MySql = config.GetSection("mySql").Get<MySqlSettings>() ?? new MySqlSettings();
            WarcraftLogs = config.GetSection("warcraftLogs").Get<WarcraftLogsSettings>();
            GoogleSheets = config.GetSection("googleSheets").Get<GoogleSheetsCredentialsSettings>() ?? new GoogleSheetsCredentialsSettings();
            GoogleHealth = config.GetSection("googleHealth").Get<GoogleHealthUserSettings[]>() ?? Array.Empty<GoogleHealthUserSettings>();
            FitnessWeightSheet = config.GetSection("fitnessWeightSheet").Get<FitnessWeightSheetSettings>() ?? new FitnessWeightSheetSettings();
        }

        private static string ResolveApiSettingsPath(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.GetFullPath(configuredPath);

            var envPath = Environment.GetEnvironmentVariable("DEV_API_APPSETTINGS");
            if (!string.IsNullOrWhiteSpace(envPath))
                return Path.GetFullPath(envPath);

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var sibling = Path.GetFullPath(Path.Combine(directory.FullName, "..", "dev-api", "appsettings.json"));
                if (File.Exists(sibling))
                    return sibling;

                directory = directory.Parent;
            }

            return string.Empty;
        }
    }

    public class GuildSettings
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, ulong> Channels { get; set; } = new();
        public string[] RolesToPing { get; set; } = [];
        public GuildFeatures Features { get; set; } = new();
        public ApplicationReviewSettings Applications { get; set; } = new();
        public RaiderManagementSettings RaiderManagement { get; set; } = new();
        public DroptimizerSettings? Droptimizer { get; set; }
        public RaidReminderSettings RaidReminders { get; set; } = new();
        public GoogleSheetsSettings? GoogleSheet { get; set; }
        public ApplicationSheetSettings? ApplicationSheet { get; set; }
        public ulong[] DenyUserIds { get; set; } = Array.Empty<ulong>();
    }

    public class GuildFeatures
    {
        public bool Droptimizer { get; set; }
        public bool DroptimizerReminder { get; set; }
        public bool KeyAudit { get; set; }
        public bool ServerAvailability { get; set; }
        public bool Applications { get; set; }
        public bool RaiderManagement { get; set; }
    }

    public class RaiderManagementSettings
    {
        public string[] RestrictedRoleIds { get; set; } = [];
    }

    public class ApplicationReviewSettings
    {
        public bool AllowXing { get; set; } = true;
        public bool AllowChecking { get; set; } = true;
    }

    public class DroptimizerSettings
    {
        public string? Source { get; set; }

        // WoWAudit
        public string? Token { get; set; }

        // WoWUtils
        public string? GroupId { get; set; }
        public string? ApiKey { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class RaidReminderSettings
    {
        public bool Enabled { get; set; }
        public int MinutesBefore { get; set; } = 60;
        public bool PingRoles { get; set; } = true;
        public RaidReminderRule[] Items { get; set; } = [];
    }

    public class RaidReminderRule
    {
        public bool Enabled { get; set; } = true;
        public string ChannelId { get; set; } = string.Empty;
        public string[] RoleIds { get; set; } = [];
        public int MinutesBefore { get; set; } = 60;
        public bool PingRoles { get; set; } = true;
    }

    public class GoogleSheetsSettings
    {
        public required string Name { get; set; }
        public required string Id { get; set; }
        public required string SheetName { get; set; }
        public required string CredentialsPath { get; set; }
    }

    public class ApplicationSheetSettings
    {
        public required string Id { get; set; }
        public required string SheetName { get; set; }
        public string CredentialsPath { get; set; } = string.Empty;
    }

    public class BattleNetSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string TokenUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    public class DiscordSettings
    {
        public string Token { get; set; } = string.Empty;
        public ulong UserId { get; set; }
    }

    public class SoundboardSettings
    {
        public ulong[] UserIds { get; set; } = Array.Empty<ulong>();
        public string SoundsPath { get; set; } = string.Empty;
    }

    public class AutoReactionRule
    {
        public ulong UserId { get; set; }
        public string[] EmoteIds { get; set; } = [];
        public string[] ProfilePictureGifUrls { get; set; } = [];
    }

    public class MySqlSettings
    {
        public string ConnectionString { get; set; } = "Server=localhost;Port=3306;Database=dev_bot;Uid=root;Pwd=;";
    }

    public class WarcraftLogsSettings
    {
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
        public WarcraftLogsZone[] Zones { get; set; } = Array.Empty<WarcraftLogsZone>();
    }

    public class GoogleSheetsCredentialsSettings
    {
        public string CredentialsPath { get; set; } = string.Empty;
    }

    public class WarcraftLogsZone
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }

    public class FitnessWeightSheetSettings
    {
        public string CredentialsPath { get; set; } = string.Empty;
    }

    public class GoogleHealthUserSettings
    {
        public string Username { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public ulong ChannelId { get; set; }
        public bool Enabled { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public double? HighestWeightLbs { get; set; }
        public string WeightSheetId { get; set; } = string.Empty;
        public string WeightSheetName { get; set; } = string.Empty;
        public string WeightSheetDateColumn { get; set; } = string.Empty;
        public string WeightSheetWeightColumn { get; set; } = string.Empty;
    }
}







