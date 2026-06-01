using MySqlConnector;

namespace dev_library.Data
{
    public class TcgHiddenItem
    {
        public int Id { get; set; }
        public string Game { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
    }

    public interface ITcgHiddenItemRepository
    {
        void EnsureTable();
        List<TcgHiddenItem> GetAll(string? game = null);
        void Hide(string game, string store, string productName);
        void Unhide(string game, string store, string productName);
    }

    public class TcgHiddenItemRepository : ITcgHiddenItemRepository
    {
        private readonly string _connectionString;

        public TcgHiddenItemRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_hidden_items (
                    id           INT AUTO_INCREMENT PRIMARY KEY,
                    game         VARCHAR(50)  NOT NULL,
                    store        VARCHAR(100) NOT NULL,
                    product_name VARCHAR(400) NOT NULL,
                    created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY uq_game_store_product (game, store, product_name)
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public List<TcgHiddenItem> GetAll(string? game = null)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, game, store, product_name
                FROM tcg_hidden_items
                WHERE (@game IS NULL OR game = @game)
                ORDER BY game, store, product_name
                """;
            cmd.Parameters.AddWithValue("@game", (object?)game ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            var list = new List<TcgHiddenItem>();
            while (reader.Read())
            {
                list.Add(new TcgHiddenItem
                {
                    Id = reader.GetInt32("id"),
                    Game = reader.GetString("game"),
                    Store = reader.GetString("store"),
                    ProductName = reader.GetString("product_name"),
                });
            }
            return list;
        }

        public void Hide(string game, string store, string productName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT IGNORE INTO tcg_hidden_items (game, store, product_name)
                VALUES (@game, @store, @productName)
                """;
            cmd.Parameters.AddWithValue("@game", game.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@store", store.Trim());
            cmd.Parameters.AddWithValue("@productName", productName.Trim());
            cmd.ExecuteNonQuery();
        }

        public void Unhide(string game, string store, string productName)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM tcg_hidden_items
                WHERE game = @game AND store = @store AND product_name = @productName
                """;
            cmd.Parameters.AddWithValue("@game", game.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@store", store.Trim());
            cmd.Parameters.AddWithValue("@productName", productName.Trim());
            cmd.ExecuteNonQuery();
        }
    }
}
