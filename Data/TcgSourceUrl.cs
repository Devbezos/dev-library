using MySqlConnector;

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

        private static readonly (string Store, string Game, string Category, string Url)[] _defaults =
        [
            ("401Games", "pokemon", "Booster Boxes", "https://store.401games.ca/collections/pokemon-trading-cards?sort=price_max_to_min&filters=Product+Type,Product+Type_Booster+Boxes,Price_from_to,66-400,In+Stock,True"),
            ("401Games", "pokemon", "New Releases", "https://store.401games.ca/collections/pokemon-new-releases?sort=price_max_to_min&filters=In+Stock,True,Category,Pokemon+Sealed+Product"),
            ("Hobbiesville", "pokemon", "Pre-Order", "https://hobbiesville.com/collections/pokemon-pre-orders-1"),
            ("Atlas", "pokemon", "Booster Boxes", "https://www.atlascollectables.com/catalog/pokemon-pokemon_sealed_products-pokemon_booster_boxes/386?filter_by_stock=in-stock"),
            ("Atlas", "gundam", "Booster Boxes", "https://www.atlascollectables.com/catalog/gundam_card_game-gundam_card_game__sealed-gundam_card_game__booster_boxes/16227?filter_by_stock=in-stock"),
            ("Chimera", "pokemon", "Pokemon Collection", "https://chimeragamingonline.com/collections/pokemon?filter.v.availability=1&filter.v.price.gte=20&filter.v.price.lte=&page={0}"),
            ("DarkFoxTCG", "pokemon", "Pokemon Sealed Product", "https://www.darkfoxtcg.com/collections/pokemon-sealed-product?product_line=All&sort=Sales&limit=30&shopify_collection_id=270727676057&min_price=20"),
            ("Dollys", "pokemon", "ETBs", "https://www.dollys.ca/catalog/pokemon_products-pokemon_elite_trainer_boxes/6218?filter_by_stock=in-stock"),
            ("Dollys", "pokemon", "Booster Boxes", "https://www.dollys.ca/catalog/pokemon_products-pokemon_booster_boxes/4033?filter_by_stock=in-stock"),
            ("Dollys", "pokemon", "Box Sets / Bundles", "https://www.dollys.ca/catalog/pokemon_products-pokemon_box_sets/3473?filter_by_stock=in-stock"),
            ("Dollys", "gundam", "Booster Boxes", "https://www.dollys.ca/catalog/gundam_card_game_products-gundam_card_game_booster_boxes/6764?filter_by_stock=in-stock"),
            ("EBGames", "pokemon", "Pokemon Search", "https://www.ebgames.ca/SearchResult/QuickSearch?q=Pok%C3%A9mon%20&platform=361&rootGenre=99&shippingMethod=1&release=1&page={0}"),
            ("EnterTheBattlefield", "pokemon", "Pokemon Sealed", "https://enterthebattlefield.ca/collections/pokemon-sealed?product_line=All&sort=Sales&limit=30&shopify_collection_id=297793978539&min_price=20"),
            ("HouseOfCards", "pokemon", "Booster Boxes", "https://houseofcards.ca/collections/pokemon-booster-boxes"),
            ("JJ", "pokemon", "Booster Boxes", "https://shop.jjcards.com/search.asp?keyword=pokemon+booster+box&catid="),
            ("TopShelfCo", "pokemon", "Other Pokemon", "https://topshelfco.ca/collections/other-pokemon"),
            ("TopShelfCo", "gundam", "Bandai Gundam CG", "https://topshelfco.ca/collections/bandai-gundam-cg"),
            ("Untouchables", "gundam", "Gundam Card Game", "https://untouchables.ca/collections/gundam-card-game"),
            ("Walmart", "pokemon", "Pokemon Cards", "https://www.walmart.ca/en/browse/toys/trading-cards/pokemon-cards/10011_31745_6000204969672?facet=fulfillment_method%3ADelivery%7C%7Cretailer_type%3AWalmart"),
        ];

        public void EnsureTable()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tcg_source_urls (
                    id         INT AUTO_INCREMENT PRIMARY KEY,
                    store      VARCHAR(100)  NOT NULL,
                    game       VARCHAR(50)   NOT NULL,
                    category   VARCHAR(200)  NOT NULL,
                    url        VARCHAR(2000) NOT NULL,
                    enabled    TINYINT(1)    NOT NULL DEFAULT 1,
                    created_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
                )
                """;
            cmd.ExecuteNonQuery();

            // Avoid MySQL key-length overflow: index a URL prefix instead of full VARCHAR(2000).
            using var idxExists = conn.CreateCommand();
            idxExists.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.statistics
                WHERE table_schema = DATABASE()
                  AND table_name = 'tcg_source_urls'
                  AND index_name = 'uq_store_game_url'
                """;
            var hasIndex = Convert.ToInt32(idxExists.ExecuteScalar()) > 0;
            if (!hasIndex)
            {
                using var addIndex = conn.CreateCommand();
                addIndex.CommandText = """
                    CREATE UNIQUE INDEX uq_store_game_url
                    ON tcg_source_urls (store, game, url(255))
                    """;
                addIndex.ExecuteNonQuery();
            }

            foreach (var d in _defaults)
            {
                using var seed = conn.CreateCommand();
                seed.CommandText = """
                    INSERT IGNORE INTO tcg_source_urls (store, game, category, url, enabled)
                    VALUES (@store, @game, @category, @url, 1)
                    """;
                seed.Parameters.AddWithValue("@store", d.Store);
                seed.Parameters.AddWithValue("@game", d.Game);
                seed.Parameters.AddWithValue("@category", d.Category);
                seed.Parameters.AddWithValue("@url", d.Url);
                seed.ExecuteNonQuery();
            }
        }

        public List<TcgSourceUrl> GetAll(string? game = null, string? store = null, bool enabledOnly = false)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, store, game, category, url, enabled
                FROM tcg_source_urls
                WHERE (@game IS NULL OR game = @game)
                  AND (@store IS NULL OR store = @store)
                  AND (@enabledOnly = 0 OR enabled = 1)
                ORDER BY store, game, category, id
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

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tcg_source_urls (store, game, category, url, enabled)
                VALUES (@store, @game, @category, @url, @enabled)
                """;
            cmd.Parameters.AddWithValue("@store", sourceUrl.Store);
            cmd.Parameters.AddWithValue("@game", sourceUrl.Game);
            cmd.Parameters.AddWithValue("@category", sourceUrl.Category);
            cmd.Parameters.AddWithValue("@url", sourceUrl.Url);
            cmd.Parameters.AddWithValue("@enabled", sourceUrl.Enabled ? 1 : 0);
            cmd.ExecuteNonQuery();
            return (int)cmd.LastInsertedId;
        }

        public void UpdateUrl(int id, string url)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tcg_source_urls SET url = @url WHERE id = @id";
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void UpdateEnabled(int id, bool enabled)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE tcg_source_urls SET enabled = @enabled WHERE id = @id";
            cmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM tcg_source_urls WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
