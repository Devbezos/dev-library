namespace dev_library.Data
{
    public static class Constants
    {
        public static class Jobs
        {
            public const string FitnessDaily        = "fitness_daily";
            public const string FitnessWeekly       = "fitness_weekly";
            public const string DroptimizerReminder = "droptimizer_reminder";
            public const string ServerAvailability  = "server_availability";
            public const string KeyAudit            = "key_audit";
            public const string Tcg                 = "tcg";
            public const string PokemonTcg          = "pokemon_tcg";
            public const string GundamTcg           = "gundam_tcg";
            public const string PreorderTcg         = "preorder_tcg";
            public const string PokemonPreorderTcg  = "pokemon_preorder_tcg";
            public const string GundamPreorderTcg   = "gundam_preorder_tcg";
            public const string PokemonCenterSecurity = "pokemon_center_security";
        }

        public static class GoogleHealth
        {
            public const string TokenUrl = "https://oauth2.googleapis.com/token";
            public const string BaseUrl  = "https://health.googleapis.com/v4";
        }

        public static class WoW
        {
            public const int MaxKeyLevel = 10;
            public static readonly string[] ErrorMessages = ["Upgrade All Equipped Gear to the Same Level"];

            public static class RaiderIo
            {
                public const string Url = "https://raider.io/api/v1";
            }

            public static class WoWAudit
            {
                public const string Url = "https://wowaudit.com/v1";
            }

            public static class WoWUtils
            {
                public const string BaseUrl = "https://wowutils.com/viserio-cooldowns";
            }

            public static class BattleNet
            {
                public const string RealmDataEndpoint = "/connected-realm/61?namespace=dynamic-us&locale=en_US";
                public const string AllRealmsEndpoint  = "/connected-realm/index?namespace=dynamic-us&locale=en_US";
                public const string ItemNameEndpoint   = "/item/{0}?namespace=static-us&locale=en_US";
            }

            public static class WarcraftLogs
            {
                public const string TokenUrl = "https://www.warcraftlogs.com/oauth/token";
                public const string ApiUrl   = "https://www.warcraftlogs.com/api/v2/client";
            }

            public static class RaidBots
            {
                public const string CacheName   = "wowcache.json";
                public const string FileUrlBase = "https://raidbots.com/reports/{0}/data.csv";
            }
        }
    }
}
