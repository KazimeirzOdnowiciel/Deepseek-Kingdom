using Dalamud.Bindings.ImGui;
using Glasswork;
using Glasswork.Backend;

namespace PluginName.Views;

/// <summary>
/// Draws the main window's body from <see cref="MainViewState"/> and nothing else.
/// </summary>
/// <remarks>
/// No Dalamud service is touched here, and no wall clock: a preview renders this with fixed sample
/// data, so anything time- or machine-dependent would make the golden unstable. Read time from
/// <c>Frame.Time</c> if the view ever needs it.
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
            Gw.Heading("Plugin Name"u8);

            if (Gw.BeginSection("Status"u8, "Everything here is safe to check while in a duty."u8))
            {
                Gw.BeginRow("Logged in"u8);
                Gw.Text(state.LoggedIn ? "yes"u8 : "no"u8, TextRole.Body);
                Gw.EndRow();

                Gw.BeginRow("Enabled"u8, "Applies immediately"u8);
                var enabled = state.Enabled;
                Gw.Toggle(ref enabled, "##enabled"u8);
                state.Enabled = enabled;
                Gw.EndRow();

                Gw.BeginRow("Volume"u8);
                ImGui.SetNextItemWidth(170f * Frame.Scale);
                var volume = state.Volume;
                Gw.Slider(ref volume, 0f, 1f, "##volume"u8, 0.01f, "0%");
                state.Volume = volume;
                Gw.EndRow();
            }

            Gw.EndSection();

            if (Gw.BeginSection("Actions"u8))
            {
                if (Gw.Button("Settings"u8))
                {
                    state.SettingsRequested = true;
                }

                if (Gw.Button("Probe item sheet"u8, ButtonStyle.Prominent))
                {
                    state.ProbeRequested = true;
                }
            }

            Gw.EndSection();

            ImGui.TextDisabled(state.ConfigPath);
        }

        Gw.EndCard();

        state.Committed = Gw.Committed;
    }
}
