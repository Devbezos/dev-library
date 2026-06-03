using MySqlConnector;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace dev_library.Data
{
    public class TcgSourceUrl
    {
        public int Id { get; set; }
        public string Store { get; set; } = string.Empty;
        public string Game { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    public interface ITcgSourceUrlRepository
    {
        void EnsureTable();
        List<TcgSourceUrl> GetAll(string? game = null, string? store = null, bool enabledOnly = false);
        int Add(TcgSourceUrl sourceUrl);
        void UpdateUrl(int id, string url);
        void UpdateEnabled(int id, bool enabled);
        void Delete(int id);
    }

    public class TcgSourceUrlRepository : ITcgSourceUrlRepository
    {
        private readonly string _connectionString;

        public TcgSourceUrlRepository(string connectionString) => _connectionString = connectionString;

        // Defaults are seeded from an external JSON file `tcg_seed_source_urls.json`
        // placed next to the application's base directory. This avoids hard-coding
        // store URLs in source and allows editing the seed without recompiling.

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            // Create normalized store and urls tables.
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_stores (
                    id          INT AUTO_INCREMENT PRIMARY KEY,
                    name        VARCHAR(100) NOT NULL UNIQUE,
                    display_name VARCHAR(200) NOT NULL,
                    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS tcg_store_urls (
                    id         INT AUTO_INCREMENT PRIMARY KEY,
                    store_id   INT NOT NULL,
                    game       VARCHAR(50)   NOT NULL,
                    category   VARCHAR(200)  NOT NULL,
                    url        VARCHAR(2000) NOT NULL,
                    enabled    TINYINT(1)    NOT NULL DEFAULT 1,
                    created_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (store_id) REFERENCES tcg_stores(id) ON DELETE CASCADE
                );
                """;
            cmd.ExecuteNonQuery();

            // Unique index on store_id+game+url prefix to avoid key-length issues.
            using var idxExists = conn.CreateCommand();
            idxExists.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.statistics
                WHERE table_schema = DATABASE()
                  AND table_name = 'tcg_store_urls'
                  AND index_name = 'uq_store_game_url'
                """;
            var hasIndex = Convert.ToInt32(idxExists.ExecuteScalar()) > 0;
            if (!hasIndex)
            {
                using var addIndex = conn.CreateCommand();
                addIndex.CommandText = """
                    CREATE UNIQUE INDEX uq_store_game_url
                    ON tcg_store_urls (store_id, game, url(255))
                    """;
                addIndex.ExecuteNonQuery();
            }

            // Seed stores and urls from an external JSON file if present.
            try
            {
                var seedPath = Path.Combine(AppContext.BaseDirectory, "tcg_seed_source_urls.json");
                if (File.Exists(seedPath))
                {
                    var json = File.ReadAllText(seedPath);
                    var seedItems = JsonSerializer.Deserialize<List<SeedEntry>>(json) ?? new List<SeedEntry>();
                    foreach (var d in seedItems)
                    {
                        using var seedStore = conn.CreateCommand();
                        seedStore.CommandText = "INSERT IGNORE INTO tcg_stores (name, display_name) VALUES (@name, @display_name)";
                        seedStore.Parameters.AddWithValue("@name", d.Store);
                        seedStore.Parameters.AddWithValue("@display_name", d.Store);
                        seedStore.ExecuteNonQuery();

                        using var seedUrl = conn.CreateCommand();
                        seedUrl.CommandText = "INSERT IGNORE INTO tcg_store_urls (store_id, game, category, url, enabled) VALUES ((SELECT id FROM tcg_stores WHERE name = @store LIMIT 1), @game, @category, @url, 1)";
                        seedUrl.Parameters.AddWithValue("@store", d.Store);
                        seedUrl.Parameters.AddWithValue("@game", d.Game);
                        seedUrl.Parameters.AddWithValue("@category", d.Category);
                        seedUrl.Parameters.AddWithValue("@url", d.Url);
                        seedUrl.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TcgSourceUrlRepository.EnsureTable: failed to seed from JSON: {ex.Message}");
            }
        }

        public List<TcgSourceUrl> GetAll(string? game = null, string? store = null, bool enabledOnly = false)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT u.id, s.name AS store, u.game, u.category, u.url, u.enabled
                FROM tcg_store_urls u
                JOIN tcg_stores s ON u.store_id = s.id
                WHERE (@game IS NULL OR u.game = @game)
                  AND (@store IS NULL OR s.name = @store)
                  AND (@enabledOnly = 0 OR u.enabled = 1)
                ORDER BY s.name, u.game, u.category, u.id
                """;
            cmd.Parameters.AddWithValue("@game", (object?)game ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@store", (object?)store ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@enabledOnly", enabledOnly ? 1 : 0);

            using var reader = cmd.ExecuteReader();
            var items = new List<TcgSourceUrl>();
            while (reader.Read())
            {
                items.Add(new TcgSourceUrl
                {
                    Id = reader.GetInt32("id"),
                    Store = reader.GetString("store"),
                    Game = reader.GetString("game"),
                    Category = reader.GetString("category"),
                    Url = reader.GetString("url"),
                    Enabled = reader.GetBoolean("enabled"),
                });
            }
            return items;
        }

        public int Add(TcgSourceUrl sourceUrl)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                // Ensure store exists.
                using var storeCmd = conn.CreateCommand();
                storeCmd.Transaction = tx;
                storeCmd.CommandText = "INSERT IGNORE INTO tcg_stores (name, display_name) VALUES (@name, @display)";
                storeCmd.Parameters.AddWithValue("@name", sourceUrl.Store);
                storeCmd.Parameters.AddWithValue("@display", sourceUrl.Store);
                storeCmd.ExecuteNonQuery();

                // Get store id.
                using var sid = conn.CreateCommand();
                sid.Transaction = tx;
                sid.CommandText = "SELECT id FROM tcg_stores WHERE name = @name LIMIT 1";
                sid.Parameters.AddWithValue("@name", sourceUrl.Store);
                var storeId = Convert.ToInt32(sid.ExecuteScalar());

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO tcg_store_urls (store_id, game, category, url, enabled) VALUES (@store_id, @game, @category, @url, @enabled)";
                cmd.Parameters.AddWithValue("@store_id", storeId);
                cmd.Parameters.AddWithValue("@game", sourceUrl.Game);
                cmd.Parameters.AddWithValue("@category", sourceUrl.Category);
                cmd.Parameters.AddWithValue("@url", sourceUrl.Url);
                cmd.Parameters.AddWithValue("@enabled", sourceUrl.Enabled ? 1 : 0);
                cmd.ExecuteNonQuery();
                var id = (int)cmd.LastInsertedId;
                tx.Commit();
                return id;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private sealed record SeedEntry(string Store, string Game, string Category, string Url);

        public void UpdateUrl(int id, string url)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tcg_store_urls SET url = @url WHERE id = @id";
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void UpdateEnabled(int id, bool enabled)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tcg_store_urls SET enabled = @enabled WHERE id = @id";
            cmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM tcg_store_urls WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
