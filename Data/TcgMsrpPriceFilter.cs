using System.Globalization;
using System.Text.RegularExpressions;

namespace dev_library.Data
{
    public static class TcgMsrpPriceFilter
    {
        private static readonly Regex PriceRegex = new(@"[-+]?\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);

        public static List<Search> HideOverDoubleMsrp(List<Search> results, IEnumerable<TcgProductGroup> productGroups)
        {
            var msrpByGroupKey = BuildMsrpLookup(productGroups);

            if (msrpByGroupKey.Count == 0) return results;

            var output = new List<Search>();
            foreach (var search in results)
            {
                var products = search.Products
                    .Where(p => !IsOverDoubleMsrp(p.Name, p.Price, msrpByGroupKey))
                    .ToList();

                if (products.Count > 0)
                    output.Add(new Search(search.Keyword, search.Store, products));
            }

            return output;
        }

        public static List<TcgResult> HideOverDoubleMsrp(IEnumerable<TcgResult> results, IEnumerable<TcgProductGroup> productGroups)
        {
            var msrpByGroupKey = BuildMsrpLookup(productGroups);

            if (msrpByGroupKey.Count == 0) return results.ToList();

            return results
                .Where(r => !IsOverDoubleMsrp(r.ProductName, r.Price, msrpByGroupKey))
                .ToList();
        }

        private static bool IsOverDoubleMsrp(string productName, string priceText, IReadOnlyDictionary<string, decimal> msrpByGroupKey)
        {
            var groupKey = TcgProductGroupRepository.NormalizeGroupKey(productName);
            if (string.IsNullOrWhiteSpace(groupKey)) return false;
            if (!msrpByGroupKey.TryGetValue(groupKey, out var msrp)) return false;
            if (!TryParsePrice(priceText, out var price)) return false;

            return price > msrp * 2m;
        }

        private static Dictionary<string, decimal> BuildMsrpLookup(IEnumerable<TcgProductGroup> productGroups) =>
            productGroups
                .Where(g => g.Msrp is > 0 && !string.IsNullOrWhiteSpace(g.GroupKey))
                .GroupBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Msrp!.Value, StringComparer.OrdinalIgnoreCase);

        private static bool TryParsePrice(string value, out decimal price)
        {
            var match = PriceRegex.Match(value);
            if (!match.Success)
            {
                price = 0m;
                return false;
            }

            return decimal.TryParse(
                match.Value.Replace(",", string.Empty),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out price);
        }
    }
}
