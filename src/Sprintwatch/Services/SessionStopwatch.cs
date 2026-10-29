using System;
using System.Collections.Generic;

namespace Sprintwatch.Services;

/// <summary>
/// A session stopwatch whose elapsed time is the difference between monotonic clock reads, never a
/// per-frame accumulation. Pure and Dalamud-free, so every transition is unit tested (T1).
/// </summary>
/// <remarks>
/// <para>
/// The caller injects both clocks. In the plugin the monotonic one is
/// <c>Stopwatch.GetTimestamp</c>/<c>Stopwatch.GetElapsedTime</c> - the system's own monotonic clock -
/// and the wall clock is <see cref="DateTimeOffset.UtcNow"/>. In tests they are plain variables, which
/// is what makes a five-minute sprint and a paused hour instantaneous to exercise.
/// </para>
/// <para>
/// A monotonic clock that steps backwards is treated as zero progress rather than a negative
/// interval, so <see cref="Elapsed"/> is never negative. The type is not thread-safe on purpose: it
/// is owned by the UI thread.
/// </para>
/// </remarks>
internal sealed class SessionStopwatch
{
    /// <summary>
    /// The most laps the ledger keeps. Adding one when the ledger is full drops the oldest lap and
    /// renumbers the survivors, so the table stays bounded for a session that runs all day.
    /// </summary>
    public const int MaxLaps = 50;

    private readonly Func<TimeSpan> monotonicNow;
    private readonly Func<DateTimeOffset> wallClockNow;
    private readonly List<LapEntry> laps = new();

    private SessionPhase phase;
    private TimeSpan completed;
    private TimeSpan? runningSince;

    /// <summary>
    /// Creates a stopwatch reading time from <paramref name="monotonicNow"/> and stamping laps with
    /// <paramref name="wallClockNow"/>.
    /// </summary>
    public SessionStopwatch(Func<TimeSpan> monotonicNow, Func<DateTimeOffset> wallClockNow)
    {
        this.monotonicNow = monotonicNow;
        this.wallClockNow = wallClockNow;
    }

    /// <summary>The current state of the stopwatch.</summary>
    public SessionPhase Phase => phase;

    /// <summary>
    /// Total running time, excluding time spent paused. While running this is a fresh difference of
    /// two monotonic reads, so it cannot drift the way a per-frame sum does.
    /// </summary>
    public TimeSpan Elapsed => completed + RunningDelta();

    /// <summary>The laps recorded so far, oldest first. Empty after <see cref="Reset"/>.</summary>
    public IReadOnlyList<LapEntry> Laps => laps;

    /// <summary>True when starting (or resuming) is meaningful.</summary>
    public bool CanStart => phase != SessionPhase.Running;

    /// <summary>True while the clock is running.</summary>
    public bool CanStop => phase == SessionPhase.Running;

    /// <summary>True while the clock is running and a lap can be stamped against it.</summary>
    public bool CanLap => phase == SessionPhase.Running;

    /// <summary>True when there is something to discard: a running or paused clock, or any lap.</summary>
    public bool CanReset => phase != SessionPhase.Idle || completed > TimeSpan.Zero || laps.Count > 0;

    /// <summary>
    /// Starts the clock from idle, or resumes it from a pause. Calling it while already running is
    /// a no-op, so a double click cannot reset the start instant or double-count the interval.
    /// </summary>
    public void Start()
    {
        if (phase == SessionPhase.Running)
        {
            return;
        }

        runningSince = monotonicNow();
        phase = SessionPhase.Running;
    }

    /// <summary>
    /// Stops the clock, freezing the elapsed time. Calling it while not running is a no-op, so the
    /// paused elapsed time cannot be changed twice.
    /// </summary>
    public void Stop()
    {
        if (phase != SessionPhase.Running)
        {
            return;
        }

        completed += RunningDelta();
        runningSince = null;
        phase = SessionPhase.Paused;
    }

    /// <summary>Discards the elapsed time and the whole lap ledger, returning to idle.</summary>
    public void Reset()
    {
        phase = SessionPhase.Idle;
        completed = TimeSpan.Zero;
        runningSince = null;
        laps.Clear();
    }

    /// <summary>
    /// Records a lap if the clock is running. The returned lap's number is its position in the
    /// ledger, and when the ledger already holds <see cref="MaxLaps"/> entries the oldest is dropped
    /// and every survivor is renumbered so the numbers stay contiguous from one.
    /// </summary>
    /// <returns>False when the clock is not running; otherwise true.</returns>
    public bool TryAddLap()
    {
        if (!CanLap)
        {
            return false;
        }

        var total = Elapsed;
        var previousTotal = laps.Count > 0 ? laps[laps.Count - 1].Total : TimeSpan.Zero;
        var split = total - previousTotal;

        if (split < TimeSpan.Zero)
        {
            // Only reachable if the monotonic clock stepped backwards between two laps. A negative
            // split would render as a negative duration, so the lap is recorded as instantaneous.
            split = TimeSpan.Zero;
        }

        laps.Add(new LapEntry(laps.Count + 1, split, total, wallClockNow().ToUniversalTime()));

        if (laps.Count > MaxLaps)
        {
            laps.RemoveAt(0);
            Renumber();
        }

        return true;
    }

    private TimeSpan RunningDelta()
    {
        if (runningSince is not { } since)
        {
            return TimeSpan.Zero;
        }

        var delta = monotonicNow() - since;
        return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
    }

    private void Renumber()
    {
        for (var i = 0; i < laps.Count; i++)
        {
            if (laps[i].Number != i + 1)
            {
                laps[i] = laps[i] with { Number = i + 1 };
            }
        }
    }
}
