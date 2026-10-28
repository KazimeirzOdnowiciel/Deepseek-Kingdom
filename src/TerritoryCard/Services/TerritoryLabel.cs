using System.Globalization;

namespace TerritoryCard.Services;

/// <summary>
/// The one place that decides what the territory row says, so the view never hard-codes it.
/// </summary>
internal static class TerritoryLabel
{
    /// <summary>The territory row's text while no character is logged in.</summary>
    public const string LoggedOut = "Not logged in";

    /// <summary>
    /// A display name for the current territory: the PlaceName text when there is one, otherwise
    /// the row id in the <c>#1234</c> form the log and the chat status line also use.
    /// </summary>
    /// <remarks>
    /// Apostrophes and leading articles are kept as the sheet gives them ("The Wanderer's Palace"),
    /// because that is what the game itself calls the place; only surrounding whitespace is removed.
    /// </remarks>
    public static string Resolve(string? placeName, uint territoryId)
    {
        if (!string.IsNullOrWhiteSpace(placeName))
        {
            return placeName.Trim();
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{territoryId}");
    }
}
