namespace dev_library_tests.Tests;

public class TcgMsrpPriceFilterTests
{
    [Fact]
    public void HideOverDoubleMsrp_RemovesSearchProductsAboveDoubleMsrp()
    {
        var results = new List<Search>
        {
            new("Pokemon", "Store", new List<Product>
            {
                new("Mega Evolution Booster Box", "$399.99", "https://example.com/ok"),
                new("Mega Evolution Booster Box", "$441.00", "https://example.com/high"),
            })
        };
        var groups = new[]
        {
            new TcgProductGroup
            {
                GroupKey = TcgProductGroupRepository.NormalizeGroupKey("Mega Evolution Booster Box"),
                Msrp = 220m,
            }
        };

        var filtered = TcgMsrpPriceFilter.HideOverDoubleMsrp(results, groups);

        Assert.Single(filtered);
        Assert.Single(filtered[0].Products);
        Assert.Equal("$399.99", filtered[0].Products[0].Price);
    }

    [Fact]
    public void HideOverDoubleMsrp_KeepsItemsWithoutSavedMsrp()
    {
        var results = new[]
        {
            new TcgResult
            {
                ProductName = "Unknown Box",
                Price = "$999.99",
            }
        };

        var filtered = TcgMsrpPriceFilter.HideOverDoubleMsrp(results, []);

        Assert.Single(filtered);
    }
}
