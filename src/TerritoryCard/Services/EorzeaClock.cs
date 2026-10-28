using System;
using System.Globalization;

namespace TerritoryCard.Services;

/// <summary>
/// Derives and formats Eorzea time without touching Dalamud, so every rule here is unit tested.
/// </summary>
/// <remarks>
/// One Eorzea day is 70 real minutes, so the ratio is 86400 ET seconds per 4 200 000 real
/// milliseconds, which reduces exactly to 18/875. The arithmetic is integer and floored, and it is
/// always fed a UTC instant: reading the local clock (or a <see cref="TimeZoneInfo"/>) here would
/// make the result depend on the machine, and the golden's determinism along with it.
/// </remarks>
internal static class EorzeaClock
{
    /// <summary>Real minutes in one Eorzea day. Also the divisor of the ratio below.</summary>
    public const int RealMinutesPerEorzeaDay = 70;

    /// <summary>86400 ET seconds over 4 200 000 real milliseconds, reduced.</summary>
    private const long EtSecondsNumerator = 18;

    private const long EtSecondsDenominator = 875;

    private const int MinutesPerDay = 1440;

    /// <summary>
    /// The Eorzea time at a Unix timestamp in UTC milliseconds, floored to the ET minute.
    /// </summary>
    /// <remarks>
    /// <c>unixMilliseconds * 18</c> stays well inside <see cref="long"/> for every instant a client
    /// can report (about 3e13 at the current epoch), so no intermediate can overflow.
    /// </remarks>
    public static EorzeaTime FromUnixMilliseconds(long unixMilliseconds)
    {
        var totalSeconds = unixMilliseconds * EtSecondsNumerator / EtSecondsDenominator;
        var minuteOfDay = (int)(totalSeconds / 60 % MinutesPerDay);

        return new EorzeaTime(minuteOfDay / 60, minuteOfDay % 60);
    }

    /// <summary>
    /// The Eorzea time at an instant, normalised to UTC first so an offset-carrying value is
    /// interpreted as the instant it denotes rather than as its wall-clock reading.
    /// </summary>
    public static EorzeaTime FromUtc(DateTimeOffset utcInstant)
        => FromUnixMilliseconds(utcInstant.ToUnixTimeMilliseconds());

    /// <summary>
    /// Formats a time of day as <c>HH:MM</c>, or <c>H:MM AM/PM</c> when <paramref name="use24HourClock"/>
    /// is false, in the invariant culture so the golden and the tests never depend on the machine's
    /// regional settings.
    /// </summary>
    public static string Format(EorzeaTime time, bool use24HourClock)
    {
        if (use24HourClock)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{time.Hour:D2}:{time.Minute:D2}");
        }

        var hour = time.Hour % 12;

        if (hour == 0)
        {
            hour = 12;
        }

        var meridiem = time.Hour < 12 ? "AM" : "PM";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hour}:{time.Minute:D2} {meridiem}");
    }
}
