using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork.Backend;
using Lumina.Excel.Sheets;
using PluginName.Views;

namespace PluginName.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const float Radius = 22f;

    private readonly Plugin plugin;
    private readonly MainView view = new();
    private readonly MainViewState state = new();
    private readonly string configPath;

    /// <summary>
    /// <c>NoBackground</c> stops ImGui painting its own fill: the glass is the background.
    /// The <c>###</c> suffix is the stable ImGui id, so renaming the visible title never
    /// resets a user's window position.
    /// </summary>
    public MainWindow(Plugin plugin)
        : base("Plugin Name###PluginNameMain", ImGuiWindowFlags.NoBackground)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 240),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
        configPath = Plugin.PluginInterface.ConfigDirectory.FullName;
        IsOpen = plugin.Configuration.MainWindowOpen;
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
        plugin.Configuration.MainWindowOpen = true;
        plugin.Configuration.Save();
        base.OnOpen();
    }

    public override void OnClose()
    {
        plugin.Configuration.MainWindowOpen = false;
        plugin.Configuration.Save();
        base.OnClose();
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
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "MainWindow.Draw failed.");
        }
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

        state.LoggedIn = Plugin.ClientState.IsLoggedIn;
        state.Enabled = config.Enabled;
        state.Volume = config.Volume;
        state.ConfigPath = configPath;
        state.Committed = false;
        state.SettingsRequested = false;
        state.ProbeRequested = false;

        view.Draw(state);

        var changed = state.Enabled != config.Enabled || Math.Abs(state.Volume - config.Volume) > 0.0001f;

        if (changed)
        {
            config.Enabled = state.Enabled;
            config.Volume = state.Volume;
        }

        if (changed || state.Committed)
        {
            config.Save();
        }

        if (state.SettingsRequested)
        {
            plugin.ToggleConfigUi();
        }

        if (state.ProbeRequested)
        {
            ProbeItemSheet();
        }
    }

    /// <summary>
    /// Game data always comes from Lumina via IDataManager, never from an HTTP API.
    /// Inside a duty a sheet read can still throw, so it fails soft and logs.
    /// </summary>
    private void ProbeItemSheet()
    {
        try
        {
            var rows = Plugin.Data.GetExcelSheet<Item>().Count();
            Plugin.ChatGui.Print($"[{Plugin.DisplayName}] Item sheet: {rows} rows.");
            Gw.Toast("Item sheet read"u8, ToastKind.Success);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Item sheet probe failed.");
            Gw.Toast("Item sheet read failed - see the log"u8, ToastKind.Error);
        }
    }
}
