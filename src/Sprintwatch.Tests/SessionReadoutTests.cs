using System;
using Sprintwatch.Services;
using Xunit;

namespace Sprintwatch.Tests;

/// <summary>
/// The readout is the only thing the view sees, so the builder's job is checked here: no lap string
/// is formatted for a hidden table, and every row carries exactly the invariant text the formatters
/// produce for its entry.
/// </summary>
public sealed class SessionReadoutTests
{
    private static readonly DateTimeOffset WallStart = new(2026, 1, 1, 13, 5, 0, TimeSpan.Zero);

    [Fact]
    public void VisibleLapsBecomeRowsWithTheContractFormatting()
    {
        var clock = new TestClock { WallClock = WallStart };
        var stopwatch = clock.CreateStopwatch();
        stopwatch.Start();

        clock.Monotonic = TimeSpan.FromSeconds(10);
        Assert.True(stopwatch.TryAddLap());

        clock.Monotonic = TimeSpan.FromSeconds(25);
        clock.WallClock = WallStart.AddSeconds(25);
        Assert.True(stopwatch.TryAddLap());

        var readout = SessionReadoutBuilder.Build(
            stopwatch,
            true,
            HourFormat.TwentyFourHour,
            TimeZoneInfo.Utc);

        Assert.True(readout.ShowLaps);
        Assert.Equal(DurationFormat.Elapsed(stopwatch.Elapsed), readout.ElapsedText);
        Assert.Equal(DurationFormat.Elapsed(TimeSpan.FromSeconds(25)), readout.TotalText);

        Assert.Collection(
            readout.Laps,
            first =>
            {
                Assert.Equal(1, first.Number);
                Assert.Equal("1", first.NumberText);
                Assert.Equal(DurationFormat.Elapsed(TimeSpan.FromSeconds(10)), first.SplitText);
                Assert.Equal(DurationFormat.Elapsed(TimeSpan.FromSeconds(10)), first.TotalText);
                Assert.Equal(
                    LapClockFormat.Format(WallStart, HourFormat.TwentyFourHour, TimeZoneInfo.Utc),
                    first.ClockText);
            },
            second =>
            {
                Assert.Equal(2, second.Number);
                Assert.Equal("2", second.NumberText);
                Assert.Equal(DurationFormat.Elapsed(TimeSpan.FromSeconds(15)), second.SplitText);
                Assert.Equal(DurationFormat.Elapsed(TimeSpan.FromSeconds(25)), second.TotalText);
                Assert.Equal(
                    LapClockFormat.Format(WallStart.AddSeconds(25), HourFormat.TwentyFourHour, TimeZoneInfo.Utc),
                    second.ClockText);
            });
    }

    [Fact]
    public void HiddenLapsAreNotFormatted()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();
        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        Assert.True(stopwatch.TryAddLap());

        var readout = SessionReadoutBuilder.Build(
            stopwatch,
            false,
            HourFormat.TwentyFourHour,
            TimeZoneInfo.Utc);

        Assert.False(readout.ShowLaps);
        Assert.Empty(readout.Laps);
        Assert.Equal(DurationFormat.Elapsed(stopwatch.Elapsed), readout.ElapsedText);
    }

    [Fact]
    public void TheRequestedHourFormatReachesTheLapRows()
    {
        var clock = new TestClock { WallClock = WallStart };
        var stopwatch = clock.CreateStopwatch();
        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        Assert.True(stopwatch.TryAddLap());

        var twentyFourHour = SessionReadoutBuilder.Build(
            stopwatch,
            true,
            HourFormat.TwentyFourHour,
            TimeZoneInfo.Utc);

        var twelveHour = SessionReadoutBuilder.Build(
            stopwatch,
            true,
            HourFormat.TwelveHour,
            TimeZoneInfo.Utc);

        Assert.Equal(
            LapClockFormat.Format(WallStart, HourFormat.TwentyFourHour, TimeZoneInfo.Utc),
            twentyFourHour.Laps[0].ClockText);

        Assert.Equal(
            LapClockFormat.Format(WallStart, HourFormat.TwelveHour, TimeZoneInfo.Utc),
            twelveHour.Laps[0].ClockText);

        Assert.NotEqual(twentyFourHour.Laps[0].ClockText, twelveHour.Laps[0].ClockText);
    }

    [Fact]
    public void TheReadoutMirrorsTheStopwatchCapabilities()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        var idle = SessionReadoutBuilder.Build(stopwatch, true, HourFormat.TwentyFourHour, TimeZoneInfo.Utc);

        Assert.Equal(stopwatch.CanStart, idle.CanStart);
        Assert.Equal(stopwatch.CanStop, idle.CanStop);
        Assert.Equal(stopwatch.CanLap, idle.CanLap);
        Assert.Equal(stopwatch.CanReset, idle.CanReset);

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(5);

        var running = SessionReadoutBuilder.Build(stopwatch, true, HourFormat.TwentyFourHour, TimeZoneInfo.Utc);

        Assert.Equal(stopwatch.CanStart, running.CanStart);
        Assert.Equal(stopwatch.CanStop, running.CanStop);
        Assert.Equal(stopwatch.CanLap, running.CanLap);
        Assert.Equal(stopwatch.CanReset, running.CanReset);
    }
}
