namespace PluginName.Views;

/// <summary>
/// Everything the main view draws, and everything it reports back.
/// </summary>
/// <remarks>
/// The view reads and writes only this object, never a Dalamud service and never the config
/// directly. That is what lets the preview project draw the same view with fixed sample data and
/// compare the result against a golden image (see CONTRIBUTING.md T6).
/// </remarks>
internal sealed class MainViewState
{
    public bool LoggedIn { get; set; }

    public bool Enabled { get; set; }

    public float Volume { get; set; }

    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>True on the frame a value control finished an edit.</summary>
    public bool Committed { get; set; }

    public bool SettingsRequested { get; set; }

    public bool ProbeRequested { get; set; }
}
