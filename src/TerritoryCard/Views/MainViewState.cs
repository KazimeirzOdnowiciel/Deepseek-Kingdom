namespace TerritoryCard.Views;

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

    /// <summary>The territory row's text, never null; the window resolves it through TerritoryLabel.</summary>
    public string TerritoryName { get; set; } = string.Empty;

    /// <summary>The formatted clock, and exactly the string the Copy button puts on the clipboard.</summary>
    public string ClockText { get; set; } = string.Empty;

    /// <summary>True on the frame the Copy ET button was pressed.</summary>
    public bool CopyRequested { get; set; }

    /// <summary>True on the frame the Settings button was pressed.</summary>
    public bool SettingsRequested { get; set; }
}
