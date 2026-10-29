namespace Sprintwatch.Services;

/// <summary>How a lap's wall-clock time is rendered.</summary>
internal enum HourFormat
{
    /// <summary>24-hour clock, <c>00:00:00</c> through <c>23:59:59</c>.</summary>
    TwentyFourHour,

    /// <summary>12-hour clock, <c>12:00:00 AM</c> through <c>11:59:59 PM</c>.</summary>
    TwelveHour,
}
