using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork.Backend;
using Lumina.Excel.Sheets;
using TerritoryCard.Services;
using TerritoryCard.Views;

namespace TerritoryCard.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const float Radius = 22f;

    private readonly Plugin plugin;
    private readonly MainView view = new();
    private readonly MainViewState state = new();

    private Throttle? throttle;
    private int throttleIntervalMs;
    private uint cachedTerritoryId;
    private string territoryName = TerritoryLabel.LoggedOut;
    private string clockText;
    private int clockHour = -1;
    private int clockMinute = -1;
    private bool clockUses24Hour;
    private bool territoryDirty = true;
    private bool loggedFailure;
    private bool loggedDrawFailure;
    private bool loggedSaveFailure;

    /// <summary>
    /// <c>NoBackground</c> stops ImGui painting its own fill: the glass is the background.
    /// The <c>###</c> suffix is the stable ImGui id, so renaming the visible title never
    /// resets a user's window position.
    /// </summary>
    public MainWindow(Plugin plugin)
        : base("Territory Card###TerritoryCardMain", ImGuiWindowFlags.NoBackground)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
        IsOpen = plugin.Configuration.MainWindowOpen;

        // A formatted string before the first Draw. This touches no game data - Eorzea time is
        // derived from UTC and runs whether or not a character is logged in.
        clockText = EorzeaClock.Format(
            EorzeaClock.FromUtc(DateTimeOffset.UtcNow),
            plugin.Configuration.Use24HourClock);
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        // The glass follows this rounding; it is in logical units and scales with the UI.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Radius * plugin.Host.Scale);
        base.PreDraw();
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        base.PostDraw();
    }

    public override void OnOpen()
    {
        PersistOpenState(true);
        base.OnOpen();
    }

    public override void OnClose()
    {
        PersistOpenState(false);
        base.OnClose();
    }

    /// <summary>
    /// Persists the open state. Dalamud calls <c>OnOpen</c>/<c>OnClose</c> from inside
    /// <c>WindowSystem.Draw()</c>, outside the guard around <see cref="Draw"/>, so a config file
    /// write that throws here aborts the draw pass and takes the plugin's UI down for the session.
    /// The write therefore fails soft and logged (N4); the in-memory value is kept either way, so a
    /// failed save costs a stale file, not the click.
    /// </summary>
    private void PersistOpenState(bool open)
    {
        plugin.Configuration.MainWindowOpen = open;

        try
        {
            plugin.Configuration.Save();
        }
        catch (Exception ex)
        {
            // Error on the transition into failure, Debug on repeats: a read-only or locked config
            // directory would otherwise log on every toggle.
            if (loggedSaveFailure)
            {
                Plugin.Log.Debug(ex, "Could not persist MainWindowOpen.");
                return;
            }

            loggedSaveFailure = true;
            Plugin.Log.Error(ex, "Could not persist MainWindowOpen.");
            return;
        }

        if (loggedSaveFailure)
        {
            loggedSaveFailure = false;
            Plugin.Log.Debug("Could persist MainWindowOpen again.");
        }
    }

    /// <summary>
    /// Draw is called every frame from Dalamud's UI thread. An exception that escapes it can
    /// take the whole plugin UI down, so the body is isolated and the failure logged.
    /// </summary>
    public override void Draw()
    {
        try
        {
            DrawBody();
            loggedDrawFailure = false;
        }
        catch (Exception ex)
        {
            // Log the transition into failure at Error and later frames at Debug: a Draw that throws
            // every frame must not write sixty stack traces a second during a duty (N4).
            if (loggedDrawFailure)
            {
                Plugin.Log.Debug(ex, "MainWindow.Draw is still failing.");
            }
            else
            {
                loggedDrawFailure = true;
                Plugin.Log.Error(ex, "MainWindow.Draw failed.");
            }
        }
    }

    /// <summary>
    /// Drops the cached name so the next sample re-reads the sheet. Called from the
    /// <c>TerritoryChanged</c> and <c>Logout</c> handlers, which only set flags.
    /// </summary>
    public void InvalidateTerritory() => territoryDirty = true;

    /// <summary>
    /// One chat line for the <c>status</c> subcommand, read fresh rather than from the cached
    /// frame state so it stays correct while the card is closed.
    /// </summary>
    public string DescribeStatus()
    {
        var clock = EorzeaClock.Format(
            EorzeaClock.FromUtc(DateTimeOffset.UtcNow),
            plugin.Configuration.Use24HourClock);

        if (!Plugin.ClientState.IsLoggedIn)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{TerritoryLabel.LoggedOut.ToLowerInvariant()}, ET {clock}");
        }

        var territoryId = Plugin.ClientState.TerritoryType;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ResolveTerritoryName(territoryId)} (territory {territoryId}), ET {clock}");
    }

    /// <summary>
    /// Gathers the game's state into <see cref="MainViewState"/>, draws the view, then applies
    /// what the view reported. The view never reads the config or a service, which is what makes
    /// it previewable.
    /// </summary>
    private void DrawBody()
    {
        var config = plugin.Configuration;

        // Records one glass callback that refracts whatever is behind the window - the game
        // and other plugins' windows included. Falls back to a flat fill when the device is
        // not ready or the GPU budget is spent, and never throws.
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        state.CopyRequested = false;
        state.SettingsRequested = false;

        // At most one sample per configured interval, never per frame: the ET minute changes
        // every ~2.9 s, so anything faster only allocates.
        if (EnsureThrottle(config.RefreshIntervalMs).TryPass(Environment.TickCount64))
        {
            Sample(config);
        }

        state.LoggedIn = Plugin.ClientState.IsLoggedIn;
        state.TerritoryName = state.LoggedIn ? territoryName : TerritoryLabel.LoggedOut;
        state.ClockText = clockText;

        view.Draw(state);

        if (state.CopyRequested)
        {
            CopyClockToClipboard();
        }

        if (state.SettingsRequested)
        {
            plugin.ToggleConfigUi();
        }
    }

    /// <summary>
    /// The throttle is re-created when the configured interval changes, so a shorter interval
    /// applies immediately instead of after the old gap.
    /// </summary>
    private Throttle EnsureThrottle(int intervalMs)
    {
        if (throttle is null || intervalMs != throttleIntervalMs)
        {
            throttle = new Throttle(intervalMs);
            throttleIntervalMs = intervalMs;
        }

        return throttle;
    }

    private void Sample(Configuration config)
    {
        var loggedIn = Plugin.ClientState.IsLoggedIn;

        if (!loggedIn)
        {
            // No sheet read at all while logged out: the logout text is not a stale sheet.
            territoryName = TerritoryLabel.LoggedOut;
            cachedTerritoryId = 0;
            territoryDirty = false;
        }
        else
        {
            var territoryId = Plugin.ClientState.TerritoryType;

            if (territoryDirty || territoryId != cachedTerritoryId)
            {
                cachedTerritoryId = territoryId;
                territoryName = ResolveTerritoryName(territoryId);
                territoryDirty = false;
            }
        }

        var time = EorzeaClock.FromUtc(DateTimeOffset.UtcNow);

        // Re-format only when the drawn text would actually differ: one allocation per ET minute
        // (~2.9 s), not one per frame.
        if (time.Hour != clockHour || time.Minute != clockMinute || config.Use24HourClock != clockUses24Hour)
        {
            clockHour = time.Hour;
            clockMinute = time.Minute;
            clockUses24Hour = config.Use24HourClock;
            clockText = EorzeaClock.Format(time, config.Use24HourClock);
        }
    }

    /// <summary>
    /// Game data always comes from Lumina via IDataManager, never from an HTTP API. The read is
    /// wrapped because the sheets are not ready on a loading screen, and an empty PlaceName row is
    /// common - hence <c>ValueNullable</c> and never <c>Value</c>, which throws for those rows.
    /// </summary>
    private string ResolveTerritoryName(uint territoryId)
    {
        try
        {
            var sheet = Plugin.Data.GetExcelSheet<TerritoryType>();

            if (!sheet.TryGetRow(territoryId, out var row))
            {
                return TerritoryLabel.Resolve(null, territoryId);
            }

            var placeName = row.PlaceName.ValueNullable;
            var name = placeName?.Name.ToString();

            if (loggedFailure)
            {
                loggedFailure = false;
                Plugin.Log.Debug("Territory sheet read recovered.");
            }

            return TerritoryLabel.Resolve(name, territoryId);
        }
        catch (Exception ex)
        {
            // Log the transition into failure, not every frame of it: a territory that cannot be
            // read must not spam the log at sixty lines a second.
            if (!loggedFailure)
            {
                loggedFailure = true;
                Plugin.Log.Debug(
                    ex,
                    "Could not read the TerritoryType row {TerritoryId}; showing the row id instead.",
                    territoryId);
            }

            return TerritoryLabel.Resolve(null, territoryId);
        }
    }

    /// <summary>
    /// Puts exactly the string on screen on the clipboard, so the two can never drift, and
    /// confirms it with a toast. A clipboard failure is a toast and a log line, never a thrown frame.
    /// </summary>
    private void CopyClockToClipboard()
    {
        var text = state.ClockText;

        try
        {
            ImGui.SetClipboardText(text);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Could not copy the Eorzea time to the clipboard.");
            Gw.Toast("Could not copy the clock", ToastKind.Error);
            return;
        }

        Gw.Toast($"Copied ET {text}", ToastKind.Success);
    }
}
