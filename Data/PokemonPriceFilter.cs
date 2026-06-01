namespace dev_library.Data
{
    /// <summary>
    /// Splits Pokemon TCG search results into normal and expensive buckets.
    /// Products priced above 200% of the matched base price are considered expensive.
    /// Store names suffixed with " 💸 Expensive" are detected by DiscordClient.PostWebHook
    /// and wrapped in spoiler tags so they appear collapsed.
    /// </summary>
    public static class PokemonPriceFilter
    {
        // ── Price tier configuration ──────────────────────────────────────────────
        // Keywords are matched case-insensitively as substrings of the product name.
        // Rules are evaluated top-to-bottom; first match wins.
        private record Tier(string[] Keywords, decimal BasePrice);

        private static readonly Tier[] Tiers =
        [
            new(["elite trainer box", "etb"],         80m),
            new(["booster bundle",    "bundle box"],  60m),
            new(["booster box"],                     220m),
        ];

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Takes combined TCG search results, splits expensive products out into
        /// separate Search entries (store name suffixed with " 💸 Expensive"), and
        /// returns the full list with normal items first, expensive items at the end.
        /// Products with no matching tier are always kept in the normal section.
        /// </summary>
        public static List<Search> Apply(List<Search> results)
        {
            var normal    = new List<Search>();
            var expensive = new List<Search>();

            foreach (var search in results)
            {
                var normalProducts    = new List<Product>();
                var expensiveProducts = new List<Product>();

                foreach (var product in search.Products)
                {
                    if (IsExpensive(product))
                        expensiveProducts.Add(product);
                    else
                        normalProducts.Add(product);
                }

                if (normalProducts.Count > 0)
                    normal.Add(new Search(search.Keyword, search.Store, normalProducts));
                if (expensiveProducts.Count > 0)
                    expensive.Add(new Search(search.Keyword, search.Store + " 💸 Expensive", expensiveProducts));
            }

            return [.. normal, .. expensive];
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool IsExpensive(Product product)
        {
            var basePrice = GetBasePrice(product.Name);
            if (basePrice is null) return false;

            // Strip leading non-numeric characters (e.g. "$", "CAD ") to parse the price
            var numericStr = new string(product.Price.SkipWhile(c => !char.IsDigit(c)).ToArray());
            return decimal.TryParse(numericStr, System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture, out var price)
                   && price > basePrice.Value * 2m;
        }

        private static decimal? GetBasePrice(string productName)
        {
            var lower = productName.ToLowerInvariant();
            foreach (var tier in Tiers)
                if (tier.Keywords.Any(k => lower.Contains(k)))
                    return tier.BasePrice;
            return null;
        }
    }
}
