using System.Text.RegularExpressions;

namespace DevClient.Data
{
    public static class Helpers
    {
        public static string GetDifficulty(string difficulty) =>
            difficulty.ToUpper() switch
            {
                "RAID-MYTHIC"            => "Mythic Raid",
                "RAID-HEROIC"            => "Heroic Raid",
                "DUNGEON-MYTHIC10"       => "Dungeon",
                "DUNGEON-MYTHIC-WEEKLY10" => "Dungeon Vault",
                _                        => string.Empty,
            };

        public static string GetItemSlot(string itemSlot) =>
            itemSlot.ToUpper() switch
            {
                "FINGER1"       => "Ring1",
                "FINGER2"       => "Ring2",
                "MAIN_HAND"     => "Weapon",
                "OFF_HAND"      => "Offhand",
                "MISCELLANEOUS" => "Curio",
                _               => itemSlot,
            };

        public static List<string> ExtractUrls(string text)
        {
            var pattern = @"https:\/\/(www\.raidbots\.com\/simbot\/report|questionablyepic\.com\/live\/upgradereport)[^\s]*";
            var matches = Regex.Matches(text, pattern);

            var urls = new List<string>();
            foreach (Match match in matches)
            {
                urls.Add(match.Value);
            }

            return urls;
        }

        public static List<(string Url, string? Region, string? Realm, string? Character, int? CharacterId)> ExtractWarcraftLogsCharacterUrls(string text)
        {
            var results = new List<(string, string?, string?, string?, int?)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Match /character/us/realm/name
            var namePattern = @"https://(?:www\.)?warcraftlogs\.com/character/(us|eu|kr|tw)/([^/#\s?&""<>\n]+)/([^/#\s?&""<>\n]+)";
            foreach (Match match in Regex.Matches(text, namePattern, RegexOptions.IgnoreCase))
            {
                var url = match.Value.TrimEnd('/', '\\', '.');
                var region = match.Groups[1].Value.ToLower();
                var realm = match.Groups[2].Value.ToLower();
                var character = match.Groups[3].Value.ToLower();
                var key = $"{region}/{realm}/{character}";
                if (seen.Add(key))
                    results.Add((url, region, realm, character, null));
            }

            // Match /character/id/NNNNNNN
            var idPattern = @"https://(?:www\.)?warcraftlogs\.com/character/id/(\d+)";
            foreach (Match match in Regex.Matches(text, idPattern, RegexOptions.IgnoreCase))
            {
                var url = match.Value.TrimEnd('/', '\\', '.');
                var id = int.Parse(match.Groups[1].Value);
                var key = $"id/{id}";
                if (seen.Add(key))
                    results.Add((url, null, null, null, id));
            }

            return results;
        }

        public static bool IsKeyAuditTime(DateTime now) =>
            (now.DayOfWeek == DayOfWeek.Friday && now.Hour == 20 && now.Minute == 0) ||
            (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 17 && now.Minute == 0);

        public static bool IsGuildActive(GuildSettings guild, DateTime now) =>
            (guild.Droptimizer?.StartDate == null || now >= guild.Droptimizer.StartDate.Value) &&
            (guild.Droptimizer?.EndDate == null || now <= guild.Droptimizer.EndDate.Value);
    }

}





