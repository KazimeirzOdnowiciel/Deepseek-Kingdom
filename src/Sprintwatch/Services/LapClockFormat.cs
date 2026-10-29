using System;
using System.Globalization;

namespace Sprintwatch.Services;

/// <summary>
/// Formats the wall clock stamped on a lap, in the player's chosen zone and in the invariant
/// culture so the text never depends on the machine's regional settings.
/// </summary>
internal static class LapClockFormat
{
    /// <summary>
    /// Converts the UTC instant to <paramref name="zone"/> and formats it as <c>HH:mm:ss</c> or
    /// <c>h:mm:ss AM/PM</c>. Midnight renders as <c>12:00:00 AM</c> and noon as
    /// <c>12:00:00 PM</c>, never as <c>0:00:00</c>.
    /// </summary>
    public static string Format(DateTimeOffset wallClockUtc, HourFormat format, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(wallClockUtc, zone);

        if (format == HourFormat.TwentyFourHour)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{local.Hour:D2}:{local.Minute:D2}:{local.Second:D2}");
        }

        var hour = local.Hour % 12;

        if (hour == 0)
        {
            hour = 12;
        }

        var meridiem = local.Hour < 12 ? "AM" : "PM";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hour}:{local.Minute:D2}:{local.Second:D2} {meridiem}");
    }
}
