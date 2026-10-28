using Glasswork;
using Glasswork.Backend;

namespace TerritoryCard.Views;

/// <summary>
/// Draws the main window's body from <see cref="MainViewState"/> and nothing else.
/// </summary>
/// <remarks>
/// No Dalamud service is touched here, and no wall clock: a preview renders this with fixed sample
/// data, so anything time- or machine-dependent would make the golden unstable. The window formats
/// the clock and passes the finished string in, and that same string is what the clipboard gets.
///
/// The body sits in a <c>Gw.BeginCard()</c> panel rather than straight on the window, which is what
/// makes it readable both over the glass in game and over the preview's backdrop stand-in.
/// </remarks>
internal sealed class MainView
{
    public void Draw(MainViewState state)
    {
        if (Gw.BeginCard())
        {
            Gw.Heading("Territory Card"u8);

            if (Gw.BeginSection("Location"u8))
            {
                Gw.BeginRow("Territory"u8);

                // Logged out, the row drops to the dimmed footnote role so "Not logged in" reads
                // as a status rather than as the name of a place.
                Gw.Text(state.TerritoryName, state.LoggedIn ? TextRole.Title2 : TextRole.Footnote);

                Gw.EndRow();
            }

            Gw.EndSection();

            if (Gw.BeginSection("Eorzea time"u8, "One Eorzea day is 70 real minutes."u8))
            {
                Gw.BeginRow("Local ET"u8);
                Gw.Text(state.ClockText, TextRole.Mono);
                Gw.EndRow();
            }

            Gw.EndSection();

            if (Gw.BeginSection("Actions"u8))
            {
                if (Gw.Button("Copy ET"u8, ButtonStyle.Prominent))
                {
                    state.CopyRequested = true;
                }

                if (Gw.Button("Settings"u8))
                {
                    state.SettingsRequested = true;
                }
            }

            Gw.EndSection();

            if (!state.LoggedIn)
            {
                Gw.Text("The clock keeps running while you are logged out."u8, TextRole.Footnote);
            }
        }

        Gw.EndCard();
    }
}
