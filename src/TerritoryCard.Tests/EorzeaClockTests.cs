using System;
using TerritoryCard.Services;
using Xunit;

namespace TerritoryCard.Tests;

/// <summary>
/// Tests cover logic that has no Dalamud dependency. Everything that reads the game or draws
/// ImGui cannot run outside the client, so it is verified in-game instead; see CONTRIBUTING.md,
/// "Testing".
/// </summary>
public sealed class EorzeaClockTests
{
    [Theory]
    [InlineData(0L, 0, 0)]
    [InlineData(175_000L, 1, 0)]
    [InlineData(1_050_000L, 6, 0)]
    [InlineData(2_100_000L, 12, 0)]
    [InlineData(4_200_000L, 0, 0)]
    public void UnixMillisecondsDeriveTheHour(long unixMilliseconds, int expectedHour, int expectedMinute)
    {
        var time = EorzeaClock.FromUnixMilliseconds(unixMilliseconds);

        Assert.Equal(expectedHour, time.Hour);
        Assert.Equal(expectedMinute, time.Minute);
    }

    [Theory]
    [InlineData(2_916L, 0, 0)]
    [InlineData(2_917L, 0, 1)]
    [InlineData(174_999L, 0, 59)]
    [InlineData(175_001L, 1, 0)]
    public void TheEtMinuteIsFloored(long unixMilliseconds, int expectedHour, int expectedMinute)
    {
        var time = EorzeaClock.FromUnixMilliseconds(unixMilliseconds);

        Assert.Equal(expectedHour, time.Hour);
        Assert.Equal(expectedMinute, time.Minute);
    }

    [Fact]
    public void OneRealMinuteAdvancesAboutTwentyEtSeconds()
    {
        var start = EorzeaClock.FromUnixMilliseconds(0);
        var afterAMinute = EorzeaClock.FromUnixMilliseconds(60_000);

        Assert.Equal(new EorzeaTime(0, 0), start);
        Assert.Equal(new EorzeaTime(0, 20), afterAMinute);
    }

    [Fact]
    public void FromUtcUsesTheInstantNotTheWallClockReading()
    {
        var utc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var sameInstantWithAnOffset = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(new EorzeaTime(0, 0), EorzeaClock.FromUtc(utc));
        Assert.Equal(EorzeaClock.FromUtc(utc), EorzeaClock.FromUtc(sameInstantWithAnOffset));
    }

    [Theory]
    [InlineData(0, 7, true, "00:07")]
    [InlineData(14, 5, true, "14:05")]
    [InlineData(23, 59, true, "23:59")]
    [InlineData(0, 7, false, "12:07 AM")]
    [InlineData(0, 0, false, "12:00 AM")]
    [InlineData(12, 0, false, "12:00 PM")]
    [InlineData(13, 5, false, "1:05 PM")]
    [InlineData(11, 59, false, "11:59 AM")]
    public void FormattingPadsAndMapsMidnightAndNoon(int hour, int minute, bool use24HourClock, string expected)
        => Assert.Equal(expected, EorzeaClock.Format(new EorzeaTime(hour, minute), use24HourClock));
}
