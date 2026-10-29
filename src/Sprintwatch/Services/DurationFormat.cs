using System;
using System.Globalization;

namespace Sprintwatch.Services;

/// <summary>
/// Formats a duration for the readout in the invariant culture, so a lap reads the same on every
/// machine and the headless golden cannot move because of a regional setting.
/// </summary>
internal static class DurationFormat
{
    /// <summary>
    /// Formats elapsed time to hundredths of a second: <c>mm:ss.ff</c> below one hour and
    /// <c>h:mm:ss.ff</c> from one hour up, with the hours counting past 24 rather than wrapping.
    /// </summary>
    /// <remarks>
    /// Hundredths are truncated, not rounded, so the clock never shows a time that has not happened
    /// yet. A negative value (a monotonic clock stepping backwards) renders as zero.
    /// </remarks>
    public static string Elapsed(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        var hundredths = value.Milliseconds / 10;
        var minutes = value.Minutes;
        var seconds = value.Seconds;

        if (value.TotalHours >= 1)
        {
            var hours = (long)value.TotalHours;

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{hours}:{minutes:D2}:{seconds:D2}.{hundredths:D2}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minutes:D2}:{seconds:D2}.{hundredths:D2}");
    }
}
