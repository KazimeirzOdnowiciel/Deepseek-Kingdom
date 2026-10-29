using System;
using Sprintwatch.Services;

namespace Sprintwatch.Tests;

/// <summary>
/// The two clocks a <see cref="SessionStopwatch"/> is fed in tests: a monotonic one the test moves
/// by hand, and a wall clock read only when a lap is stamped. Nothing reads the machine's clock, so
/// a five-minute sprint is instantaneous to exercise and every assertion can be exact.
/// </summary>
internal sealed class TestClock
{
    public TimeSpan Monotonic { get; set; }

    public DateTimeOffset WallClock { get; set; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public SessionStopwatch CreateStopwatch()
    {
        return new SessionStopwatch(() => Monotonic, () => WallClock);
    }
}
