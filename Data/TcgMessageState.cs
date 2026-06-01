using MySqlConnector;

namespace dev_library.Data
{
    public interface ITcgMessageStateRepository
    {
        void EnsureTable();
        ulong[] GetMessageIds(ulong channelId);
        void SetMessageIds(ulong channelId, ulong[] messageIds);
    }

    public class TcgMessageStateRepository : ITcgMessageStateRepository
    {
        private readonly string _connectionString;

        public TcgMessageStateRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_message_state (
                    channel_id  BIGINT UNSIGNED NOT NULL PRIMARY KEY,
                    message_ids TEXT NOT NULL,
                    updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public ulong[] GetMessageIds(ulong channelId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT message_ids FROM tcg_message_state WHERE channel_id = @channelId LIMIT 1";
            cmd.Parameters.AddWithValue("@channelId", channelId);

            var value = cmd.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(value)) return [];

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => ulong.TryParse(s, out var id) ? id : 0)
                .Where(id => id != 0)
                .Distinct()
                .ToArray();
        }

        public void SetMessageIds(ulong channelId, ulong[] messageIds)
        {
            var cleaned = (messageIds ?? [])
                .Where(id => id != 0)
                .Distinct()
                .ToArray();

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            if (cleaned.Length == 0)
            {
                using var delete = conn.CreateCommand();
                delete.CommandText = "DELETE FROM tcg_message_state WHERE channel_id = @channelId";
                delete.Parameters.AddWithValue("@channelId", channelId);
                delete.ExecuteNonQuery();
                return;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tcg_message_state (channel_id, message_ids)
                VALUES (@channelId, @messageIds)
                ON DUPLICATE KEY UPDATE message_ids = VALUES(message_ids)
                """;
            cmd.Parameters.AddWithValue("@channelId", channelId);
            cmd.Parameters.AddWithValue("@messageIds", string.Join(',', cleaned));
            cmd.ExecuteNonQuery();
        }
    }
}