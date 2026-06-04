using MySqlConnector;
using System.Text.RegularExpressions;

namespace DevClient.Data
{
public class TcgProductGroupRepository : ITcgProductGroupRepository
    {
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
        private readonly string _connectionString;

        public TcgProductGroupRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_product_groups (
                    game         VARCHAR(50)  NOT NULL,
                    group_key    VARCHAR(300) NOT NULL,
                    display_name VARCHAR(500) NOT NULL,
                    resale_market_price DECIMAL(10,2) NULL,
                    updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (game, group_key)
                )
                """;
            cmd.ExecuteNonQuery();

            EnsureColumn(conn, "resale_market_price", "DECIMAL(10,2) NULL");
        }

        public List<TcgProductGroup> GetAll(string? game = null)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT game, group_key, display_name, resale_market_price
                FROM tcg_product_groups
                WHERE (@game IS NULL OR game = @game)
                ORDER BY game, display_name
                """;
            cmd.Parameters.AddWithValue("@game", (object?)NormalizeGame(game) ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            var groups = new List<TcgProductGroup>();
            while (reader.Read())
            {
                groups.Add(new TcgProductGroup
                {
                    Game = reader.GetString("game"),
                    GroupKey = reader.GetString("group_key"),
                    DisplayName = reader.GetString("display_name"),
                    ResaleMarketPrice = reader["resale_market_price"] == DBNull.Value ? null : reader.GetDecimal("resale_market_price"),
                });
            }
            return groups;
        }

        public void SetMarketPrices(string game, string groupKey, string displayName, decimal? resaleMarketPrice)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tcg_product_groups (game, group_key, display_name, resale_market_price)
                VALUES (@game, @groupKey, @displayName, @resaleMarketPrice)
                ON DUPLICATE KEY UPDATE display_name = @displayName, resale_market_price = @resaleMarketPrice
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@groupKey", NormalizeGroupKey(groupKey));
            cmd.Parameters.AddWithValue("@displayName", displayName.Trim());
            cmd.Parameters.AddWithValue("@resaleMarketPrice", (object?)resaleMarketPrice ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureColumn(MySqlConnection conn, string columnName, string definition)
        {
            using var exists = conn.CreateCommand();
            exists.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = 'tcg_product_groups'
                  AND column_name = @columnName
                """;
            exists.Parameters.AddWithValue("@columnName", columnName);
            var hasColumn = Convert.ToInt32(exists.ExecuteScalar()) > 0;
            if (hasColumn) return;

            using var add = conn.CreateCommand();
            add.CommandText = $"ALTER TABLE tcg_product_groups ADD COLUMN {columnName} {definition}";
            add.ExecuteNonQuery();
        }


        public static string NormalizeGroupKey(string value)
        {
            var lower = value.ToLowerInvariant();
            lower = Regex.Replace(lower, @"\([^)]*\)", " ");
            lower = Regex.Replace(lower, @"\b(pre[- ]?order|new|sealed|product|pokemon|tcg|english|display|mega|evolution)\b", " ");
            lower = Regex.Replace(lower, @"[^a-z0-9]+", " ");
            return Spaces.Replace(lower, " ").Trim();
        }

        private static string? NormalizeGame(string? game) =>
            string.IsNullOrWhiteSpace(game) ? null : game.Trim().ToLowerInvariant();
    }
}



