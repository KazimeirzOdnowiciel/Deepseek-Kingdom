using System.Collections.Generic;

namespace Sprintwatch.Services;

/// <summary>
/// Everything the main view draws, already formatted. Built once per frame by
/// <see cref="SessionReadoutBuilder"/>; the view never reads the stopwatch, the configuration or
/// the wall clock, which is what lets the headless preview draw it with fixed sample data.
/// </summary>
internal sealed record SessionReadout(
    string ElapsedText,
    string TotalText,
    IReadOnlyList<LapRow> Laps,
    bool ShowLaps,
    bool CanStart,
    bool CanStop,
    bool CanLap,
    bool CanReset);
