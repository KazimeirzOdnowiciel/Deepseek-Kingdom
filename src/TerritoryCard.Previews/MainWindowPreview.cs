using System;
using System.Numerics;
using Glasswork;
using Glasswork.Backend.Preview;
using TerritoryCard.Views;

namespace TerritoryCard.Previews;

/// <summary>
/// Draws the main window's view with fixed sample data so it can be rendered without the game
/// and compared against <c>goldens/main.png</c>.
/// </summary>
/// <remarks>
/// The sample data must be built here, never read from the config or the game, and never from the
/// wall clock - the golden only holds while every run draws the same pixels.
/// </remarks>
public sealed class MainWindowPreview : IPreviewScreen
{
    private readonly MainView view = new();

    private readonly MainViewState state = new()
    {
        LoggedIn = true,
        TerritoryName = "Central Shroud",
        ClockText = "14:05",
    };

    /// <summary>Also the golden's file name: <c>goldens/main.png</c>.</summary>
    public string Name => "main";

    /// <summary>
    /// Tall enough that the default content does not scroll: a scrollbar in a golden is noise that
    /// moves whenever anything above it changes height, and anything below the fold is not covered.
    /// </summary>
    public Vector2 WindowSize => new(420f, 470f);

    /// <summary>The Look the plugin sets on its host, so the golden shows what users see.</summary>
    public Look Look { get; } = Look.Eorzea();

    public ReadOnlySpan<PreviewInput> Script => [];

    public void Draw() => view.Draw(state);
}
