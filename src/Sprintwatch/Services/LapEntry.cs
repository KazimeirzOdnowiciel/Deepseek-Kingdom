using System;

namespace Sprintwatch.Services;

/// <summary>
/// One recorded lap: its position in the ledger, the time spent in that lap, the session total at
/// the moment it was taken, and the wall clock of that moment (always stored in UTC).
/// </summary>
/// <remarks>
/// These are raw values, not display strings. <see cref="SessionReadoutBuilder"/> is the only place
/// that formats them, so the ledger stays independent of the chosen clock and time zone.
/// </remarks>
internal readonly record struct LapEntry(
    int Number,
    TimeSpan Split,
    TimeSpan Total,
    DateTimeOffset WallClockUtc);
