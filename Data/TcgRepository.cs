using MySqlConnector;

namespace dev_library.Data
{
    public class TcgRepository : ITcgRepository
    {
        private readonly string _connectionString;

        public TcgRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_results (
                    id           INT AUTO_INCREMENT PRIMARY KEY,
                    run_at       DATETIME     NOT NULL,
                    game         VARCHAR(50)  NOT NULL DEFAULT 'pokemon',
                    store        VARCHAR(200) NOT NULL,
                    keyword      VARCHAR(500) NOT NULL,
                    product_name VARCHAR(500) NOT NULL,
                    price        VARCHAR(100) NOT NULL,
                    url          VARCHAR(2000) NOT NULL,
                    INDEX idx_run_at (run_at)
                )
                """;
            cmd.ExecuteNonQuery();

            // Migration: add game column to existing tables that predate this schema
            using var mig = conn.CreateCommand();
            mig.CommandText = """
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME   = 'tcg_results'
                  AND COLUMN_NAME  = 'game'
                """;
            var exists = Convert.ToInt64(mig.ExecuteScalar()) > 0;
            if (!exists)
            {
                using var addCol = conn.CreateCommand();
                addCol.CommandText = "ALTER TABLE tcg_results ADD COLUMN game VARCHAR(50) NOT NULL DEFAULT 'pokemon'";
                addCol.ExecuteNonQuery();
            }
        }

        public void SaveResults(DateTime runAt, List<Search> results, string game = "pokemon")
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Clear previous data for this game only
                using var del = conn.CreateCommand();
                del.Transaction = tx;
                del.CommandText = "DELETE FROM tcg_results WHERE game = @game";
                del.Parameters.AddWithValue("@game", game);
                del.ExecuteNonQuery();

                foreach (var search in results)
                {
                    foreach (var product in search.Products)
                    {
                        using var ins = conn.CreateCommand();
                        ins.Transaction = tx;
                        ins.CommandText = """
                            INSERT INTO tcg_results (run_at, game, store, keyword, product_name, price, url)
                            VALUES (@runAt, @game, @store, @keyword, @productName, @price, @url)
                            """;
                        ins.Parameters.AddWithValue("@runAt", runAt);
                        ins.Parameters.AddWithValue("@game", game);
                        ins.Parameters.AddWithValue("@store", search.Store);
                        ins.Parameters.AddWithValue("@keyword", search.Keyword);
                        ins.Parameters.AddWithValue("@productName", product.Name);
                        ins.Parameters.AddWithValue("@price", product.Price);
                        ins.Parameters.AddWithValue("@url", product.Url);
                        ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<TcgResult> GetLatestRun(string game = "pokemon")
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, run_at, game, store, keyword, product_name, price, url
                FROM tcg_results
                WHERE game = @game
                ORDER BY store, keyword, product_name
                """;
            cmd.Parameters.AddWithValue("@game", game);
            using var reader = cmd.ExecuteReader();
            var result = new List<TcgResult>();
            while (reader.Read())
                result.Add(new TcgResult
                {
                    Id          = reader.GetInt32("id"),
                    RunAt       = reader.GetDateTime("run_at"),
                    Game        = reader.GetString("game"),
                    Store       = reader.GetString("store"),
                    Keyword     = reader.GetString("keyword"),
                    ProductName = reader.GetString("product_name"),
                    Price       = reader.GetString("price"),
                    Url         = reader.GetString("url"),
                });
            return result;
        }
    }
}
