using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sprintwatch.Services;

/// <summary>
/// Turns a live <see cref="SessionStopwatch"/> into the immutable, already formatted state the view
/// draws. Pure: it reads the stopwatch (and, through the stopwatch's injected clocks) nothing else.
/// </summary>
internal static class SessionReadoutBuilder
{
    /// <summary>
    /// Builds one frame's readout. <paramref name="showLaps"/> false leaves
    /// <see cref="SessionReadout.Laps"/> empty, so no lap string is formatted for a table that is
    /// not drawn.
    /// </summary>
    /// <remarks>
    /// <see cref="SessionReadout.ElapsedText"/> is the live session clock.
    /// <see cref="SessionReadout.TotalText"/> is the ledger's cumulative total at the most recently
    /// recorded lap - the same value as that lap row's total - or zero before the first lap. It
    /// matches the table's "Total" column rather than repeating the live clock.
    /// </remarks>
    public static SessionReadout Build(
        SessionStopwatch stopwatch,
        bool showLaps,
        HourFormat format,
        TimeZoneInfo zone)
    {
        var laps = stopwatch.Laps;
        var elapsed = stopwatch.Elapsed;
        var total = laps.Count > 0 ? laps[laps.Count - 1].Total : TimeSpan.Zero;

        return new SessionReadout(
            DurationFormat.Elapsed(elapsed),
            DurationFormat.Elapsed(total),
            showLaps ? FormatLaps(laps, format, zone) : Array.Empty<LapRow>(),
            showLaps,
            stopwatch.CanStart,
            stopwatch.CanStop,
            stopwatch.CanLap,
            stopwatch.CanReset);
    }

    private static LapRow[] FormatLaps(
        IReadOnlyList<LapEntry> laps,
        HourFormat format,
        TimeZoneInfo zone)
    {
        var rows = new LapRow[laps.Count];

        for (var i = 0; i < laps.Count; i++)
        {
            var lap = laps[i];

            rows[i] = new LapRow(
                lap.Number,
                lap.Number.ToString(CultureInfo.InvariantCulture),
                DurationFormat.Elapsed(lap.Split),
                DurationFormat.Elapsed(lap.Total),
                LapClockFormat.Format(lap.WallClockUtc, format, zone));
        }

        return rows;
    }
}
