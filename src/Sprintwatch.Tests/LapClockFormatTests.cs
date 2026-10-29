using System;
using System.Globalization;
using Sprintwatch.Services;
using Xunit;

namespace Sprintwatch.Tests;

/// <summary>
/// Lap stamps are formatted from a UTC instant and an explicit zone in the invariant culture, so
/// these tests never depend on the machine's clock, zone or regional settings.
/// </summary>
public sealed class LapClockFormatTests
{
    [Theory]
    [InlineData(0, 0, false, "00:00:00")]
    [InlineData(0, 0, true, "12:00:00 AM")]
    [InlineData(12, 0, false, "12:00:00")]
    [InlineData(12, 0, true, "12:00:00 PM")]
    [InlineData(13, 5, false, "13:05:00")]
    [InlineData(13, 5, true, "1:05:00 PM")]
    [InlineData(23, 59, false, "23:59:00")]
    [InlineData(23, 59, true, "11:59:00 PM")]
    public void MidnightAndNoonAreTheEdgesTheTwelveHourClockMoves(
        int hour,
        int minute,
        bool twelveHour,
        string expected)
    {
        var instant = new DateTimeOffset(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);
        var format = twelveHour ? HourFormat.TwelveHour : HourFormat.TwentyFourHour;

        Assert.Equal(expected, LapClockFormat.Format(instant, format, TimeZoneInfo.Utc));
    }

    [Fact]
    public void SecondsAreKept()
    {
        var instant = new DateTimeOffset(2026, 1, 1, 13, 5, 9, TimeSpan.Zero);

        Assert.Equal("13:05:09", LapClockFormat.Format(instant, HourFormat.TwentyFourHour, TimeZoneInfo.Utc));
        Assert.Equal("1:05:09 PM", LapClockFormat.Format(instant, HourFormat.TwelveHour, TimeZoneInfo.Utc));
    }

    [Fact]
    public void TheZoneConvertsTheInstantBeforeItIsFormatted()
    {
        // 23:30 UTC is 01:30 the next day at UTC+2: 1:30 AM, not 11:30 PM.
        // .NET only offers custom zones with the standard/daylight display names, so pass both.
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone(
            "SprintwatchTestPlusTwo",
            TimeSpan.FromHours(2),
            "Test UTC+2",
            "Test UTC+2");

        var instant = new DateTimeOffset(2026, 1, 1, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("1:30:00 AM", LapClockFormat.Format(instant, HourFormat.TwelveHour, plusTwo));
        Assert.Equal("01:30:00", LapClockFormat.Format(instant, HourFormat.TwentyFourHour, plusTwo));
    }

    [Fact]
    public void FormattingDoesNotUseTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            // Under a culture with non-Latin meridiem designators, a formatter that forgot
            // CultureInfo.InvariantCulture would produce different text here.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            var instant = new DateTimeOffset(2026, 1, 1, 13, 5, 0, TimeSpan.Zero);

            Assert.Equal("1:05:00 PM", LapClockFormat.Format(instant, HourFormat.TwelveHour, TimeZoneInfo.Utc));
            Assert.Equal("13:05:00", LapClockFormat.Format(instant, HourFormat.TwentyFourHour, TimeZoneInfo.Utc));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
