using Microsoft.Extensions.Configuration;

namespace dev_library.Data
{
    public static class AppSettings
    {
        public static bool DryRun { get; set; }
        public static DiscordSettings Discord { get; set; }
        public static BattleNetSettings BattleNet { get; set; }
        public static GuildSettings[] Guilds { get; set; }
        public static SoundboardSettings Soundboard { get; set; } = new();
        public static string BasePath { get; set; } = $"{Path.GetPathRoot(AppContext.BaseDirectory)}Code";

        public static MySqlSettings MySql { get; set; } = new();
        public static WarcraftLogsSettings? WarcraftLogs { get; set; }
        public static GoogleHealthUserSettings[] GoogleHealth { get; set; } = Array.Empty<GoogleHealthUserSettings>();

        public static void Initialize()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            DryRun = config.GetValue<bool>("dryRun");
            Discord = config.GetSection("discord").Get<DiscordSettings>();
            BattleNet = config.GetSection("battleNet").Get<BattleNetSettings>();
            Guilds = config.GetSection("guilds").Get<GuildSettings[]>() ?? Array.Empty<GuildSettings>();
            Soundboard = config.GetSection("soundboard").Get<SoundboardSettings>() ?? new SoundboardSettings();
            MySql = config.GetSection("mySql").Get<MySqlSettings>() ?? new MySqlSettings();
            WarcraftLogs = config.GetSection("warcraftLogs").Get<WarcraftLogsSettings>();
            GoogleHealth = config.GetSection("googleHealth").Get<GoogleHealthUserSettings[]>() ?? Array.Empty<GoogleHealthUserSettings>();
        }
    }

    public class GuildSettings
    {
        public string Name { get; set; }
        public Dictionary<string, ulong> Channels { get; set; }
        public string[] RolesToPing { get; set; }
        public GuildFeatures Features { get; set; }
        public DroptimizerSettings Droptimizer { get; set; }
        public GoogleSheetsSettings GoogleSheet { get; set; }
        public ApplicationSheetSettings ApplicationSheet { get; set; }
        public ulong[] DenyUserIds { get; set; } = Array.Empty<ulong>();
    }

    public class GuildFeatures
    {
        public bool Droptimizer { get; set; }
        public bool DroptimizerReminder { get; set; }
        public bool KeyAudit { get; set; }
        public bool ServerAvailability { get; set; }
    }

    public class DroptimizerSettings
    {
        public string Token { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GoogleSheetsSettings
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string SheetName { get; set; }
        public string CredentialsPath { get; set; }
    }

    public class ApplicationSheetSettings
    {
        public string Id { get; set; }
        public string SheetName { get; set; }
    }

    public class BattleNetSettings
    {
        public string ApiUrl { get; set; }
        public string TokenUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public class DiscordSettings
    {
        public string Token { get; set; }
        public ulong UserId { get; set; }
    }

    public class SoundboardSettings
    {
        public ulong[] UserIds { get; set; } = Array.Empty<ulong>();
        public string SoundsPath { get; set; } = string.Empty;
    }

    public class MySqlSettings
    {
        public string ConnectionString { get; set; } = "Server=localhost;Port=3306;Database=dev_bot;Uid=root;Pwd=;";
    }

    public class WarcraftLogsSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public WarcraftLogsZone[] Zones { get; set; } = Array.Empty<WarcraftLogsZone>();
    }

    public class WarcraftLogsZone
    {
        public int Id { get; set; }
        public string Name { get; set; }
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
    }
}
