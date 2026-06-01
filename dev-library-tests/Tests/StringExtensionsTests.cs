namespace dev_library_tests.Tests;

public class StringExtensionsTests
{
    // ─── ToTitleCase ──────────────────────────────────────────────────────────

    [Fact]
    public void ToTitleCase_LowercaseWord_CapitalizesFirstLetter()
    {
        Assert.Equal("Hello", "hello".ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_AlreadyCapitalized_ReturnsSame()
    {
        Assert.Equal("World", "World".ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_SingleChar_ReturnsCapital()
    {
        Assert.Equal("A", "a".ToTitleCase());
    }

    [Fact]
    public void ToTitleCase_PreservesRestOfString()
    {
        Assert.Equal("Hello world", "hello world".ToTitleCase());
    }

    // ─── PadBoth ──────────────────────────────────────────────────────────────

    [Fact]
    public void PadBoth_EvenPadding_CentresString()
    {
        // "hi" (2 chars), target=6, 2 chars each side
        var result = "hi".PadBoth(6, '-');
        Assert.Equal(6, result.Length);
        Assert.Equal("--hi--", result);
    }

    [Fact]
    public void PadBoth_OddPadding_ExtraCharOnRight()
    {
        // "hi" (2 chars), target=7, 2 left / 3 right (spaces/2 integer division)
        var result = "hi".PadBoth(7, '-');
        Assert.Equal(7, result.Length);
        Assert.StartsWith("--hi", result);
    }

    [Fact]
    public void PadBoth_SameLengthAsTarget_ReturnsUnchanged()
    {
        var result = "abc".PadBoth(3, '-');
        Assert.Equal("abc", result);
    }
}
