using MySqlConnector;

namespace DevClient.Data
{
public class TcgBlacklistWordRepository : ITcgBlacklistWordRepository
    {
        private readonly string _connectionString;

        public TcgBlacklistWordRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_blacklist_words (
                    id         INT AUTO_INCREMENT PRIMARY KEY,
                    game       VARCHAR(50)  NOT NULL,
                    word       VARCHAR(200) NOT NULL,
                    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY uq_game_word (game, word)
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public List<TcgBlacklistWord> GetAll(string? game = null)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, game, word
                FROM tcg_blacklist_words
                WHERE (@game IS NULL OR game = @game)
                ORDER BY game, word
                """;
            cmd.Parameters.AddWithValue("@game", (object?)NormalizeGame(game) ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            var list = new List<TcgBlacklistWord>();
            while (reader.Read())
            {
                list.Add(new TcgBlacklistWord
                {
                    Id = reader.GetInt32("id"),
                    Game = reader.GetString("game"),
                    Word = reader.GetString("word"),
                });
            }
            return list;
        }

        public void Add(string game, string word)
        {
            var normalizedWord = NormalizeWord(word);
            if (string.IsNullOrEmpty(normalizedWord)) return;

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT IGNORE INTO tcg_blacklist_words (game, word)
                VALUES (@game, @word)
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@word", normalizedWord);
            cmd.ExecuteNonQuery();
        }

        public void Remove(string game, string word)
        {
            var normalizedWord = NormalizeWord(word);
            if (string.IsNullOrEmpty(normalizedWord)) return;

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM tcg_blacklist_words
                WHERE game = @game AND word = @word
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));
            cmd.Parameters.AddWithValue("@word", normalizedWord);
            cmd.ExecuteNonQuery();
        }

        private static string NormalizeGame(string? game) => (game ?? string.Empty).Trim().ToLowerInvariant();
        private static string NormalizeWord(string word) => (word ?? string.Empty).Trim().ToLowerInvariant();
    }
}



