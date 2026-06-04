using MySqlConnector;

namespace DevClient.Data
{
public class TcgFilterSettingsRepository : ITcgFilterSettingsRepository
    {
        public static readonly string[] DefaultPokemonTerms = ["booster box", "booster bundle", "elite trainer box", "etb", "collection"];

        private readonly string _connectionString;

        public TcgFilterSettingsRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_filter_terms (
                    id         INT AUTO_INCREMENT PRIMARY KEY,
                    game       VARCHAR(50)  NOT NULL,
                    term       VARCHAR(200) NOT NULL,
                    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY uq_game_term (game, term)
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public List<string> GetTerms(string game)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT term
                FROM tcg_filter_terms
                WHERE game = @game
                ORDER BY term
                """;
            cmd.Parameters.AddWithValue("@game", NormalizeGame(game));

            using var reader = cmd.ExecuteReader();
            var terms = new List<string>();
            while (reader.Read())
                terms.Add(reader.GetString("term"));
            return terms;
        }

        public void SetTerms(string game, IEnumerable<string> terms)
        {
            var normalizedGame = NormalizeGame(game);
            var normalizedTerms = terms
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM tcg_filter_terms WHERE game = @game";
                del.Parameters.AddWithValue("@game", normalizedGame);
                del.ExecuteNonQuery();
            }

            foreach (var term in normalizedTerms)
            {
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO tcg_filter_terms (game, term) VALUES (@game, @term)";
                ins.Parameters.AddWithValue("@game", normalizedGame);
                ins.Parameters.AddWithValue("@term", term);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public void EnsureDefault(string game, IEnumerable<string> terms)
        {
            var existing = GetTerms(game);
            if (existing.Count > 0) return;
            SetTerms(game, terms);
        }

        private static string NormalizeGame(string game) => game.Trim().ToLowerInvariant();
    }
}



