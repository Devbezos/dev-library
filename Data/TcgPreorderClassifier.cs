using System.Text.RegularExpressions;

namespace DevClient.Data;

public static class TcgPreorderClassifier
{
    private static readonly Regex PreorderRegex = new(
        @"\bpre[\s\-_/\.]*orders?\b|\bpreorders?\b|\bpre[\s\-_/\.]*sales?\b|\bpresales?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static bool IsPreorder(Product product) => IsPreorder(product.Name);

    public static bool IsPreorder(TcgResult result) => IsPreorder(result.ProductName);

    public static bool IsPreorder(string productName) =>
        !string.IsNullOrWhiteSpace(productName) && PreorderRegex.IsMatch(productName);

    public static (List<Search> Regular, List<Search> Preorders) Split(List<Search> results)
    {
        var regular = new List<Search>();
        var preorders = new List<Search>();

        foreach (var search in results)
        {
            var regularProducts = new List<Product>();
            var preorderProducts = new List<Product>();

            foreach (var product in search.Products)
            {
                if (IsPreorder(product))
                    preorderProducts.Add(product);
                else
                    regularProducts.Add(product);
            }

            if (regularProducts.Count > 0)
                regular.Add(new Search(search.Keyword, search.Store, regularProducts));
            if (preorderProducts.Count > 0)
                preorders.Add(new Search(search.Keyword, search.Store, preorderProducts));
        }

        return (regular, preorders);
    }

    public static List<Search> Merge(IEnumerable<Search> results)
    {
        return results
            .GroupBy(s => SearchKey(s.Keyword, s.Store), StringComparer.OrdinalIgnoreCase)
            .Select(g => new Search(
                g.First().Keyword,
                g.First().Store,
                g.SelectMany(s => s.Products)
                    .GroupBy(ProductKey, StringComparer.OrdinalIgnoreCase)
                    .Select(pg => pg.First())
                    .OrderBy(p => p.Name)
                    .ToList()))
            .Where(s => s.Products.Count > 0)
            .OrderBy(s => s.Store)
            .ThenBy(s => s.Keyword)
            .ToList();
    }

    public static string NormalizeGroupKey(string value)
    {
        var lower = value.ToLowerInvariant();
        lower = Regex.Replace(lower, @"\([^)]*\)", " ");
        lower = Regex.Replace(lower, @"\b(pre[- ]?order|new|sealed|product|pokemon|tcg|english|display|mega|evolution)\b", " ");
        lower = Regex.Replace(lower, @"[^a-z0-9]+", " ");
        return Spaces.Replace(lower, " ").Trim();
    }

    public static List<Search> FromTcgResults(IEnumerable<TcgResult> results)
    {
        return results
            .GroupBy(r => SearchKey(r.Keyword, r.Store), StringComparer.OrdinalIgnoreCase)
            .Select(g => new Search(
                g.First().Keyword,
                g.First().Store,
                g.Select(r =>
                {
                    var product = new Product(r.ProductName, r.Price, ExtractRawUrl(r.Url));
                    product.Url = r.Url;
                    return product;
                }).ToList()))
            .Where(s => s.Products.Count > 0)
            .ToList();
    }

    private static string ProductKey(Product product) =>
        $"{product.Name.Trim().ToLowerInvariant()}||{ExtractRawUrl(product.Url).Trim().ToLowerInvariant()}";

    private static string SearchKey(string keyword, string store) =>
        $"{keyword.Trim().ToLowerInvariant()}||{store.Trim().ToLowerInvariant()}";

    private static string ExtractRawUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var match = Regex.Match(value, @"\[.*?\]\((https?://[^)]+)\)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : value;
    }
}





