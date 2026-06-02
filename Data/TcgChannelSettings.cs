using MySqlConnector;

namespace dev_library.Data
{
    public interface ITcgChannelSettingsRepository
    {
        void EnsureTable();
        ulong GetChannelId(string game);
        ulong[] GetNotificationUserIds(string game);
        Dictionary<string, ulong> GetAll();
        void SetChannelId(string game, ulong channelId);
        void SetNotificationUserIds(string game, IEnumerable<ulong> userIds);
        void EnsureDefault(string game, ulong channelId);
    }

    public class TcgChannelSettingsRepository : ITcgChannelSettingsRepository
    {
        private readonly string _connectionString;

        public TcgChannelSettingsRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_channel_settings (
                    game        VARCHAR(50)  NOT NULL PRIMARY KEY,
                    channel_id  BIGINT UNSIGNED NOT NULL,
                    notification_user_ids TEXT NULL,
                    updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                )
                """;
            cmd.ExecuteNonQuery();
            EnsureColumn(conn, "notification_user_ids", "TEXT NULL");
        }

        public ulong GetChannelId(string game)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT channel_id FROM tcg_channel_settings WHERE game = @game LIMIT 1";
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0UL : Convert.ToUInt64(result);
        }

        public ulong[] GetNotificationUserIds(string game)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT notification_user_ids FROM tcg_channel_settings WHERE game = @game LIMIT 1";
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return [];

            return ParseUserIds(Convert.ToString(result) ?? string.Empty);
        }

        public Dictionary<string, ulong> GetAll()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT game, channel_id FROM tcg_channel_settings";
            using var reader = cmd.ExecuteReader();
            var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                map[reader.GetString("game")] = Convert.ToUInt64(reader["channel_id"]);
            }
            return map;
        }

        public void SetChannelId(string game, ulong channelId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tcg_channel_settings (game, channel_id)
                VALUES (@game, @channelId)
                ON DUPLICATE KEY UPDATE channel_id = VALUES(channel_id)
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.ExecuteNonQuery();
        }

        public void SetNotificationUserIds(string game, IEnumerable<ulong> userIds)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tcg_channel_settings (game, channel_id, notification_user_ids)
                VALUES (@game, 0, @userIds)
                ON DUPLICATE KEY UPDATE notification_user_ids = VALUES(notification_user_ids)
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@userIds", string.Join(",", userIds.Distinct()));
            cmd.ExecuteNonQuery();
        }

        public void EnsureDefault(string game, ulong channelId)
        {
            if (channelId == 0) return;

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT IGNORE INTO tcg_channel_settings (game, channel_id)
                VALUES (@game, @channelId)
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.ExecuteNonQuery();
        }

        private static string NormalizeGame(string game) => game.Trim().ToLowerInvariant();

        private static ulong[] ParseUserIds(string value) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => ulong.TryParse(v, out var id) ? id : 0UL)
                .Where(id => id != 0)
                .Distinct()
                .ToArray();

        private static void EnsureColumn(MySqlConnection conn, string columnName, string definition)
        {
            using var exists = conn.CreateCommand();
            exists.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                  AND table_name = 'tcg_channel_settings'
                  AND column_name = @columnName
                """;
            exists.Parameters.AddWithValue("@columnName", columnName);
            var hasColumn = Convert.ToInt32(exists.ExecuteScalar()) > 0;
            if (hasColumn) return;

            using var add = conn.CreateCommand();
            add.CommandText = $"ALTER TABLE tcg_channel_settings ADD COLUMN {columnName} {definition}";
            add.ExecuteNonQuery();
        }
    }
}
