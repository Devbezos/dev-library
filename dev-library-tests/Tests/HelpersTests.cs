namespace dev_library_tests.Tests;

public class HelpersTests
{
    // ─── GetDifficulty ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("RAID-MYTHIC",             "Mythic Raid")]
    [InlineData("raid-mythic",             "Mythic Raid")]
    [InlineData("RAID-HEROIC",             "Heroic Raid")]
    [InlineData("DUNGEON-MYTHIC10",        "Dungeon")]
    [InlineData("DUNGEON-MYTHIC-WEEKLY10", "Dungeon Vault")]
    [InlineData("OTHER",                   "")]
    [InlineData("",                        "")]
    public void GetDifficulty_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, Helpers.GetDifficulty(input));
    }

    // ─── GetItemSlot ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("FINGER1",       "Ring1")]
    [InlineData("FINGER2",       "Ring2")]
    [InlineData("MAIN_HAND",     "Weapon")]
    [InlineData("OFF_HAND",      "Offhand")]
    [InlineData("MISCELLANEOUS", "Curio")]
    [InlineData("HEAD",          "HEAD")]   // pass-through
    [InlineData("CHEST",         "CHEST")]
    public void GetItemSlot_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, Helpers.GetItemSlot(input));
    }

    // ─── ExtractUrls ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractUrls_NoMatches_ReturnsEmpty()
    {
        var result = Helpers.ExtractUrls("no urls here");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUrls_RaidbotsUrl_ReturnsMatch()
    {
        var url = "https://www.raidbots.com/simbot/report/abc123";
        var result = Helpers.ExtractUrls($"Check this out {url} and let me know");
        Assert.Single(result);
        Assert.Equal(url, result[0]);
    }

    [Fact]
    public void ExtractUrls_QuestionablyEpicUrl_ReturnsMatch()
    {
        var url = "https://questionablyepic.com/live/upgradereport/xyz";
        var result = Helpers.ExtractUrls(url);
        Assert.Single(result);
        Assert.Equal(url, result[0]);
    }

    [Fact]
    public void ExtractUrls_MultipleUrls_ReturnsAll()
    {
        var url1 = "https://www.raidbots.com/simbot/report/aaa";
        var url2 = "https://questionablyepic.com/live/upgradereport/bbb";
        var result = Helpers.ExtractUrls($"{url1} and {url2}");
        Assert.Equal(2, result.Count);
        Assert.Contains(url1, result);
        Assert.Contains(url2, result);
    }

    [Fact]
    public void ExtractUrls_UnrelatedHttpsUrl_ReturnsEmpty()
    {
        var result = Helpers.ExtractUrls("https://google.com/search?q=test");
        Assert.Empty(result);
    }

    // ─── ExtractWarcraftLogsCharacterUrls ────────────────────────────────────

    [Fact]
    public void ExtractWarcraftLogsCharacterUrls_ByName_ReturnsRegionRealmCharacter()
    {
        var url = "https://www.warcraftlogs.com/character/us/zuljin/herochar";
        var result = Helpers.ExtractWarcraftLogsCharacterUrls(url);

        Assert.Single(result);
        Assert.Equal("us",       result[0].Region);
        Assert.Equal("zuljin",   result[0].Realm);
        Assert.Equal("herochar", result[0].Character);
        Assert.Null(result[0].CharacterId);
    }

    [Fact]
    public void ExtractWarcraftLogsCharacterUrls_ById_ReturnsId()
    {
        var url = "https://www.warcraftlogs.com/character/id/12345678";
        var result = Helpers.ExtractWarcraftLogsCharacterUrls(url);

        Assert.Single(result);
        Assert.Equal(12345678, result[0].CharacterId);
        Assert.Null(result[0].Region);
    }

    [Fact]
    public void ExtractWarcraftLogsCharacterUrls_DuplicateUrl_DeduplicatesResult()
    {
        var url = "https://www.warcraftlogs.com/character/us/zuljin/herochar";
        var result = Helpers.ExtractWarcraftLogsCharacterUrls($"{url} {url}");
        Assert.Single(result);
    }

    [Fact]
    public void ExtractWarcraftLogsCharacterUrls_NoMatch_ReturnsEmpty()
    {
        var result = Helpers.ExtractWarcraftLogsCharacterUrls("no urls here");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractWarcraftLogsCharacterUrls_MixedUrls_ReturnsBoth()
    {
        var nameUrl = "https://www.warcraftlogs.com/character/us/aerie-peak/tankchar";
        var idUrl   = "https://www.warcraftlogs.com/character/id/99";
        var result  = Helpers.ExtractWarcraftLogsCharacterUrls($"{nameUrl} {idUrl}");
        Assert.Equal(2, result.Count);
    }

    // ─── IsGuildActive ────────────────────────────────────────────────────────

    [Fact]
    public void IsGuildActive_NullDroptimizer_ReturnsTrue()
    {
        var guild = new GuildSettings { Droptimizer = null };
        Assert.True(Helpers.IsGuildActive(guild, DateTime.UtcNow));
    }

    [Fact]
    public void IsGuildActive_NoStartOrEnd_ReturnsTrue()
    {
        var guild = new GuildSettings { Droptimizer = new DroptimizerSettings() };
        Assert.True(Helpers.IsGuildActive(guild, DateTime.UtcNow));
    }

    [Fact]
    public void IsGuildActive_BeforeStartDate_ReturnsFalse()
    {
        var guild = new GuildSettings
        {
            Droptimizer = new DroptimizerSettings { StartDate = DateTime.UtcNow.AddDays(1) }
        };
        Assert.False(Helpers.IsGuildActive(guild, DateTime.UtcNow));
    }

    [Fact]
    public void IsGuildActive_AfterEndDate_ReturnsFalse()
    {
        var guild = new GuildSettings
        {
            Droptimizer = new DroptimizerSettings { EndDate = DateTime.UtcNow.AddDays(-1) }
        };
        Assert.False(Helpers.IsGuildActive(guild, DateTime.UtcNow));
    }

    [Fact]
    public void IsGuildActive_WithinDateRange_ReturnsTrue()
    {
        var now = new DateTime(2026, 6, 15);
        var guild = new GuildSettings
        {
            Droptimizer = new DroptimizerSettings
            {
                StartDate = new DateTime(2026, 6, 1),
                EndDate   = new DateTime(2026, 6, 30)
            }
        };
        Assert.True(Helpers.IsGuildActive(guild, now));
    }
}
