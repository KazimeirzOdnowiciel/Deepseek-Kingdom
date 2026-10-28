namespace TerritoryCard.Services;

/// <summary>
/// A time of day on Eorzea's 24-hour clock, in the range 00:00 to 23:59.
/// </summary>
/// <remarks>
/// Deliberately not <c>Glasswork.Game.EorzeaTime</c>: the view needs the hour and minute so it can
/// decide whether the string it shows has to change, and the clipboard has to receive exactly the
/// string on screen. The derivation lives here so it can be unit tested without the game.
/// </remarks>
internal readonly record struct EorzeaTime(int Hour, int Minute);
