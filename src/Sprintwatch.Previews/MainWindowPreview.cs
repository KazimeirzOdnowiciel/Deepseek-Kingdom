using System;
using System.Numerics;
using Glasswork;
using Glasswork.Backend.Preview;
using Sprintwatch.Services;
using Sprintwatch.Views;

namespace Sprintwatch.Previews;

/// <summary>
/// Draws the main window's view with fixed sample data so it can be rendered without the game
/// and compared against <c>goldens/main.png</c>.
/// </summary>
/// <remarks>
/// The sample session is built from the real <see cref="SessionStopwatch"/> and
/// <see cref="SessionReadoutBuilder"/>, so the golden exercises the same formatting the game does,
/// but through injected clocks that never move. Nothing here reads the config, the game or the
/// wall clock (CONTRIBUTING T7); the same sample draws the same pixels on every run.
/// </remarks>
public sealed class MainWindowPreview : IPreviewScreen
{
    private static readonly ViewCommands Commands = new(
        NoOp,
        NoOp,
        NoOp,
        NoOp,
        IgnoreShowLaps,
        IgnoreHourFormat);

    private readonly SessionReadout readout = SampleReadout();

    /// <summary>Also the golden's file name: <c>goldens/main.png</c>.</summary>
    public string Name => "main";

    /// <summary>
    /// Tall enough that the default content does not scroll: a scrollbar in a golden is noise that
    /// moves whenever anything above it changes height, and anything below the fold is not covered.
    /// </summary>
    public Vector2 WindowSize => new(460f, 800f);

    /// <summary>The Look the plugin sets on its host, so the golden shows what users see.</summary>
    public Look Look { get; } = Look.Eorzea();

    public ReadOnlySpan<PreviewInput> Script => [];

    public void Draw() => SprintwatchView.Draw(in readout, in Commands);

    private static void NoOp()
    {
    }

    private static void IgnoreShowLaps(bool _)
    {
    }

    private static void IgnoreHourFormat(HourFormat _)
    {
    }

    /// <summary>
    /// A running session at <c>01:23.40</c> with three consistent laps (splits 12.4 s, 12.7 s and
    /// 12.8 s, totalling 37.9 s), each stamped 24-hour in UTC. Built once, when the preview is
    /// created: every later frame draws the same readout.
    /// </summary>
    private static SessionReadout SampleReadout()
    {
        var now = TimeSpan.Zero;
        var wallClock = new DateTimeOffset(2026, 1, 1, 14, 5, 0, TimeSpan.Zero);
        var stopwatch = new SessionStopwatch(() => now, () => wallClock);

        stopwatch.Start();

        now = TimeSpan.FromSeconds(12.4);
        wallClock = wallClock.AddSeconds(12.4);
        stopwatch.TryAddLap();

        now = TimeSpan.FromSeconds(25.1);
        wallClock = wallClock.AddSeconds(12.7);
        stopwatch.TryAddLap();

        now = TimeSpan.FromSeconds(37.9);
        wallClock = wallClock.AddSeconds(12.8);
        stopwatch.TryAddLap();

        now = TimeSpan.FromSeconds(83.4);

        return SessionReadoutBuilder.Build(stopwatch, showLaps: true, HourFormat.TwentyFourHour, TimeZoneInfo.Utc);
    }
}
