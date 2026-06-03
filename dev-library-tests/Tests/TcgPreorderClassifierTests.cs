public class TcgPreorderClassifierTests
{
    [Theory]
    [InlineData("Pokemon Mega Evolution Pre-Order Booster Box")]
    [InlineData("Pokemon Mega Evolution Pre Order Booster Box")]
    [InlineData("Pokemon Mega Evolution Preorder Booster Box")]
    [InlineData("Pokemon Mega Evolution pre/order Booster Box")]
    [InlineData("Pokemon Mega Evolution Presale Booster Box")]
    public void IsPreorder_MatchesPreorderVariants(string productName)
    {
        Assert.True(TcgPreorderClassifier.IsPreorder(productName));
    }

    [Fact]
    public void Split_MovesPreorderProductsOutOfRegularResults()
    {
        var results = new List<Search>
        {
            new("Pokemon", "Store", [
                new Product("In Stock Booster Box", "$149.99", "https://example.com/in-stock"),
                new Product("Pre-Order Booster Box", "$149.99", "https://example.com/preorder"),
            ]),
        };

        var split = TcgPreorderClassifier.Split(results);

        Assert.Single(split.Regular);
        Assert.Single(split.Regular[0].Products);
        Assert.Equal("In Stock Booster Box", split.Regular[0].Products[0].Name);
        Assert.Single(split.Preorders);
        Assert.Single(split.Preorders[0].Products);
        Assert.Equal("Pre-Order Booster Box", split.Preorders[0].Products[0].Name);
    }
}
