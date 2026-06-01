namespace dev_library_tests.Tests;

/// <summary>
/// Unit tests for the pure scheduling logic in <see cref="JobRepository"/>.
/// These tests exercise ShouldRun / ShouldRunToday / ShouldRunThisWeek
/// without any database interaction.
/// </summary>
public class JobRepositoryLogicTests
{
    // Use a fixed UTC timezone for deterministic timezone-aware tests
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // Factory helper – creates a JobRepository with a dummy connection string
    // (only the logic methods are called, so the connection is never opened)
    private static JobRepository Repo() => new("Server=dummy;");

    // ─── ShouldRun ────────────────────────────────────────────────────────────

    [Fact]
    public void ShouldRun_EnabledJobWithMatchingTime_ReturnsTrue()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, Hour = 12, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 12, 0, 0); // Monday
        Assert.True(Repo().ShouldRun(job, now));
    }

    [Fact]
    public void ShouldRun_DisabledJob_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = false, Hour = 12, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 12, 0, 0);
        Assert.False(Repo().ShouldRun(job, now));
    }

    [Fact]
    public void ShouldRun_WrongHour_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, Hour = 12, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 13, 0, 0);
        Assert.False(Repo().ShouldRun(job, now));
    }

    [Fact]
    public void ShouldRun_WrongMinute_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, Hour = 12, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 12, 1, 0);
        Assert.False(Repo().ShouldRun(job, now));
    }

    [Fact]
    public void ShouldRun_WithDayOfWeek_MatchingDay_ReturnsTrue()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, DayOfWeek = (int)DayOfWeek.Monday, Hour = 9, Minute = 0 };
        var monday = new DateTime(2026, 1, 5, 9, 0, 0); // Monday
        Assert.True(Repo().ShouldRun(job, monday));
    }

    [Fact]
    public void ShouldRun_WithDayOfWeek_WrongDay_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, DayOfWeek = (int)DayOfWeek.Monday, Hour = 9, Minute = 0 };
        var tuesday = new DateTime(2026, 1, 6, 9, 0, 0); // Tuesday
        Assert.False(Repo().ShouldRun(job, tuesday));
    }

    [Fact]
    public void ShouldRun_RanVeryRecently_ReturnsFalse()
    {
        var job = new ScheduledJob
        {
            Name    = "x",
            Enabled = true,
            Hour    = 12,
            Minute  = 0,
            LastRun = DateTime.UtcNow.AddSeconds(-30) // only 30s ago
        };
        var now = new DateTime(2026, 1, 5, 12, 0, 0);
        Assert.False(Repo().ShouldRun(job, now));
    }

    [Fact]
    public void ShouldRun_RanMoreThanAMinuteAgo_ReturnsTrue()
    {
        var job = new ScheduledJob
        {
            Name    = "x",
            Enabled = true,
            Hour    = 12,
            Minute  = 0,
            LastRun = DateTime.UtcNow.AddMinutes(-2)
        };
        var now = new DateTime(2026, 1, 5, 12, 0, 0);
        Assert.True(Repo().ShouldRun(job, now));
    }

    // ─── ShouldRunToday ───────────────────────────────────────────────────────

    [Fact]
    public void ShouldRunToday_Enabled_NeverRan_ReturnsTrue()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, Hour = 8, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 8, 0, 0);
        Assert.True(Repo().ShouldRunToday(job, now, Utc));
    }

    [Fact]
    public void ShouldRunToday_Disabled_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = false, Hour = 8, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 8, 0, 0);
        Assert.False(Repo().ShouldRunToday(job, now, Utc));
    }

    [Fact]
    public void ShouldRunToday_WrongHour_ReturnsFalse()
    {
        var job = new ScheduledJob { Name = "x", Enabled = true, Hour = 8, Minute = 0 };
        var now = new DateTime(2026, 1, 5, 9, 0, 0);
        Assert.False(Repo().ShouldRunToday(job, now, Utc));
    }

    [Fact]
    public void ShouldRunToday_AlreadyRanToday_ReturnsFalse()
    {
        var today = new DateTime(2026, 1, 5, 8, 0, 0);
        var job   = new ScheduledJob
        {
            Name    = "x",
            Enabled = true,
            Hour    = 8,
            Minute  = 0,
            LastRun = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc)
        };
        Assert.False(Repo().ShouldRunToday(job, today, Utc));
    }

    [Fact]
    public void ShouldRunToday_RanYesterday_ReturnsTrue()
    {
        var today = new DateTime(2026, 1, 5, 8, 0, 0);
        var job   = new ScheduledJob
        {
            Name    = "x",
            Enabled = true,
            Hour    = 8,
            Minute  = 0,
            LastRun = new DateTime(2026, 1, 4, 8, 0, 0, DateTimeKind.Utc) // yesterday
        };
        Assert.True(Repo().ShouldRunToday(job, today, Utc));
    }

    // ─── ShouldRunThisWeek ────────────────────────────────────────────────────

    [Fact]
    public void ShouldRunThisWeek_Enabled_NeverRan_OnConfiguredDay_ReturnsTrue()
    {
        // Sunday = 0
        var job    = new ScheduledJob { Name = "x", Enabled = true, DayOfWeek = (int)DayOfWeek.Sunday, Hour = 0, Minute = 0 };
        var sunday = new DateTime(2026, 1, 4, 0, 0, 0); // Sunday
        Assert.True(Repo().ShouldRunThisWeek(job, sunday, Utc));
    }

    [Fact]
    public void ShouldRunThisWeek_Disabled_ReturnsFalse()
    {
        var job    = new ScheduledJob { Name = "x", Enabled = false, DayOfWeek = (int)DayOfWeek.Sunday, Hour = 0, Minute = 0 };
        var sunday = new DateTime(2026, 1, 4, 0, 0, 0);
        Assert.False(Repo().ShouldRunThisWeek(job, sunday, Utc));
    }

    [Fact]
    public void ShouldRunThisWeek_WrongDayOfWeek_ReturnsFalse()
    {
        var job    = new ScheduledJob { Name = "x", Enabled = true, DayOfWeek = (int)DayOfWeek.Sunday, Hour = 0, Minute = 0 };
        var monday = new DateTime(2026, 1, 5, 0, 0, 0); // Monday
        Assert.False(Repo().ShouldRunThisWeek(job, monday, Utc));
    }

    [Fact]
    public void ShouldRunThisWeek_AlreadyRanThisWeek_ReturnsFalse()
    {
        // Week boundary is Sunday; job ran last Sunday (same week)
        var thisSunday = new DateTime(2026, 1, 4, 0, 0, 0);
        var job = new ScheduledJob
        {
            Name      = "x",
            Enabled   = true,
            DayOfWeek = (int)DayOfWeek.Sunday,
            Hour      = 0,
            Minute    = 0,
            LastRun   = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc) // same Sunday UTC
        };
        Assert.False(Repo().ShouldRunThisWeek(job, thisSunday, Utc));
    }

    [Fact]
    public void ShouldRunThisWeek_RanLastWeek_ReturnsTrue()
    {
        var thisSunday = new DateTime(2026, 1, 11, 0, 0, 0);
        var job = new ScheduledJob
        {
            Name      = "x",
            Enabled   = true,
            DayOfWeek = (int)DayOfWeek.Sunday,
            Hour      = 0,
            Minute    = 0,
            LastRun   = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc) // previous Sunday
        };
        Assert.True(Repo().ShouldRunThisWeek(job, thisSunday, Utc));
    }
}
