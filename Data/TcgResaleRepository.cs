using MySqlConnector;

namespace DevClient.Data
{
public class TcgResaleRepository : ITcgResaleRepository
    {
        private readonly string _connectionString;

        public TcgResaleRepository(string connectionString) => _connectionString = connectionString;

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_resale_values (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    item_name VARCHAR(500) NOT NULL,
                    avg_price DECIMAL(10,2) NULL,
                    sample_count INT DEFAULT 0,
                    currency VARCHAR(10) DEFAULT 'USD',
                    last_updated DATETIME NULL,
                    updated_by VARCHAR(100),
                    UNIQUE KEY ux_item_name (item_name(255))
                )
                """;
            cmd.ExecuteNonQuery();
        }

        public void UpsertResaleValue(string itemName, decimal? avgPrice, int sampleCount, string currency, DateTime updatedAt, string updatedBy = "job")
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT id FROM tcg_resale_values WHERE LEFT(item_name,255) = LEFT(@item_name,255) LIMIT 1";
                cmd.Parameters.AddWithValue("@item_name", itemName);
                var existing = cmd.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                {
                    var id = Convert.ToInt32(existing);
                    using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = "UPDATE tcg_resale_values SET avg_price=@avg, sample_count=@count, currency=@currency, last_updated=@lu, updated_by=@ub WHERE id=@id";
                    upd.Parameters.AddWithValue("@avg", (object?)avgPrice ?? DBNull.Value);
                    upd.Parameters.AddWithValue("@count", sampleCount);
                    upd.Parameters.AddWithValue("@currency", currency);
                    upd.Parameters.AddWithValue("@lu", updatedAt);
                    upd.Parameters.AddWithValue("@ub", updatedBy);
                    upd.Parameters.AddWithValue("@id", id);
                    upd.ExecuteNonQuery();
                }
                else
                {
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO tcg_resale_values (item_name, avg_price, sample_count, currency, last_updated, updated_by) VALUES (@item_name, @avg, @count, @currency, @lu, @ub)";
                    ins.Parameters.AddWithValue("@item_name", itemName);
                    ins.Parameters.AddWithValue("@avg", (object?)avgPrice ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@count", sampleCount);
                    ins.Parameters.AddWithValue("@currency", currency);
                    ins.Parameters.AddWithValue("@lu", updatedAt);
                    ins.Parameters.AddWithValue("@ub", updatedBy);
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<TcgResaleValue> GetLatest(int limit = 100)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, item_name, avg_price, sample_count, currency, last_updated, updated_by FROM tcg_resale_values ORDER BY last_updated DESC LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            var list = new List<TcgResaleValue>();
            while (reader.Read())
            {
                list.Add(new TcgResaleValue
                {
                    Id = reader.GetInt32("id"),
                    ItemName = reader.GetString("item_name"),
                    AvgPrice = reader.IsDBNull(reader.GetOrdinal("avg_price")) ? null : reader.GetDecimal("avg_price"),
                    SampleCount = reader.GetInt32("sample_count"),
                    Currency = reader.GetString("currency"),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("last_updated")) ? DateTime.MinValue : reader.GetDateTime("last_updated"),
                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("updated_by")) ? string.Empty : reader.GetString("updated_by"),
                });
            }
            return list;
        }
    }
}



