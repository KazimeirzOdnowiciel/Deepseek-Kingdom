using System;
using System.Linq;
using Sprintwatch.Services;
using Xunit;

namespace Sprintwatch.Tests;

/// <summary>
/// The stopwatch reads both of its clocks through injected delegates, so every transition is
/// exercised here with a hand-moved clock; the game only supplies the real ones.
/// </summary>
public sealed class SessionStopwatchTests
{
    [Fact]
    public void FreshStopwatchIsIdleAtZero()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        Assert.Equal(SessionPhase.Idle, stopwatch.Phase);
        Assert.Equal(TimeSpan.Zero, stopwatch.Elapsed);
        Assert.Empty(stopwatch.Laps);
        Assert.True(stopwatch.CanStart);
        Assert.False(stopwatch.CanStop);
        Assert.False(stopwatch.CanLap);
        Assert.False(stopwatch.CanReset);
    }

    [Fact]
    public void StartRunsAndARepeatedStartDoesNotRestartTheInterval()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        stopwatch.Start();

        Assert.Equal(SessionPhase.Running, stopwatch.Phase);
        Assert.Equal(TimeSpan.FromSeconds(10), stopwatch.Elapsed);
        Assert.False(stopwatch.CanStart);
        Assert.True(stopwatch.CanStop);
        Assert.True(stopwatch.CanLap);
        Assert.True(stopwatch.CanReset);
    }

    [Fact]
    public void StopFreezesTheElapsedTime()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        stopwatch.Stop();

        clock.Monotonic = TimeSpan.FromSeconds(30);
        stopwatch.Stop();

        Assert.Equal(SessionPhase.Paused, stopwatch.Phase);
        Assert.Equal(TimeSpan.FromSeconds(10), stopwatch.Elapsed);
        Assert.True(stopwatch.CanStart);
        Assert.False(stopwatch.CanStop);
        Assert.False(stopwatch.CanLap);
    }

    [Fact]
    public void ResumeContinuesFromThePausedElapsedTime()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        stopwatch.Stop();

        // Time passing while paused belongs to no sprint and must not be counted.
        clock.Monotonic = TimeSpan.FromSeconds(100);
        Assert.Equal(TimeSpan.FromSeconds(10), stopwatch.Elapsed);

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(130);

        Assert.Equal(TimeSpan.FromSeconds(40), stopwatch.Elapsed);
    }

    [Fact]
    public void ReadingElapsedRepeatedlyDoesNotAccumulate()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(7);

        Assert.Equal(TimeSpan.FromSeconds(7), stopwatch.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(7), stopwatch.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(7), stopwatch.Elapsed);
    }

    [Fact]
    public void ABackwardsClockNeverReportsNegativeElapsed()
    {
        var clock = new TestClock { Monotonic = TimeSpan.FromSeconds(10) };
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(20);
        Assert.Equal(TimeSpan.FromSeconds(10), stopwatch.Elapsed);

        // A monotonic clock is not supposed to step back, but a suspended or virtualised machine
        // can produce it; the elapsed time must clamp at zero instead of going negative.
        clock.Monotonic = TimeSpan.FromSeconds(5);
        Assert.True(stopwatch.Elapsed >= TimeSpan.Zero);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void LapsRecordTheirNumberSplitTotalAndWallClock()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();
        var firstWallClock = clock.WallClock.AddSeconds(10);

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        clock.WallClock = firstWallClock;
        Assert.True(stopwatch.TryAddLap());

        var secondWallClock = firstWallClock.AddSeconds(15);
        clock.Monotonic = TimeSpan.FromSeconds(25);
        clock.WallClock = secondWallClock;
        Assert.True(stopwatch.TryAddLap());

        Assert.Collection(
            stopwatch.Laps,
            first =>
            {
                Assert.Equal(1, first.Number);
                Assert.Equal(TimeSpan.FromSeconds(10), first.Split);
                Assert.Equal(TimeSpan.FromSeconds(10), first.Total);
                Assert.Equal(firstWallClock, first.WallClockUtc);
            },
            second =>
            {
                Assert.Equal(2, second.Number);
                Assert.Equal(TimeSpan.FromSeconds(15), second.Split);
                Assert.Equal(TimeSpan.FromSeconds(25), second.Total);
                Assert.Equal(secondWallClock, second.WallClockUtc);
            });
    }

    [Fact]
    public void ALapAfterABackwardsClockHasNoNegativeSplit()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        Assert.True(stopwatch.TryAddLap());

        clock.Monotonic = TimeSpan.FromSeconds(5);
        Assert.True(stopwatch.TryAddLap());

        Assert.Equal(TimeSpan.Zero, stopwatch.Laps[1].Split);
    }

    [Fact]
    public void TryAddLapIsRejectedUnlessTheClockIsRunning()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        Assert.False(stopwatch.TryAddLap());

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(5);
        stopwatch.Stop();

        Assert.False(stopwatch.TryAddLap());
        Assert.Empty(stopwatch.Laps);
    }

    [Fact]
    public void ResetReturnsToIdleAndDiscardsEveryLap()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();

        stopwatch.Start();
        clock.Monotonic = TimeSpan.FromSeconds(10);
        Assert.True(stopwatch.TryAddLap());

        stopwatch.Reset();

        Assert.Equal(SessionPhase.Idle, stopwatch.Phase);
        Assert.Equal(TimeSpan.Zero, stopwatch.Elapsed);
        Assert.Empty(stopwatch.Laps);
        Assert.True(stopwatch.CanStart);
        Assert.False(stopwatch.CanLap);
        Assert.False(stopwatch.CanReset);
    }

    [Fact]
    public void TheOldestLapIsDroppedAtCapacityAndTheSurvivorsRenumber()
    {
        var clock = new TestClock();
        var stopwatch = clock.CreateStopwatch();
        stopwatch.Start();

        for (var i = 1; i <= SessionStopwatch.MaxLaps + 1; i++)
        {
            clock.Monotonic = TimeSpan.FromSeconds(10 * i);
            Assert.True(stopwatch.TryAddLap());
        }

        var laps = stopwatch.Laps;

        // Exactly MaxLaps survivors, numbered contiguously from one; lap one's entry is gone.
        Assert.Equal(Enumerable.Range(1, SessionStopwatch.MaxLaps), laps.Select(lap => lap.Number));

        var oldestSurvivor = laps[0];
        Assert.Equal(TimeSpan.FromSeconds(10), oldestSurvivor.Split);
        Assert.Equal(TimeSpan.FromSeconds(20), oldestSurvivor.Total);

        var newest = laps[SessionStopwatch.MaxLaps - 1];
        Assert.Equal(TimeSpan.FromSeconds(10), newest.Split);
        Assert.Equal(TimeSpan.FromSeconds(510), newest.Total);
    }
}
