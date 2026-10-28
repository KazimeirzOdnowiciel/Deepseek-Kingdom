using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork;
using Glasswork.Backend;
using Lumina.Excel.Sheets;

namespace PluginName.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const float Radius = 22f;

    private readonly Plugin plugin;

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

    private void DrawBody()
    {
        // Records one glass callback that refracts whatever is behind the window - the game
        // and other plugins' windows included. Falls back to a flat fill when the device is
        // not ready or the GPU budget is spent, and never throws.
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        Gw.Heading("Plugin Name"u8);

        if (Gw.BeginSection("Status"u8, "Everything here is safe to check while in a duty."u8))
        {
            Gw.BeginRow("Logged in"u8);
            Gw.Text(Plugin.ClientState.IsLoggedIn ? "yes"u8 : "no"u8, TextRole.Body);
            Gw.EndRow();

            Gw.BeginRow("Enabled"u8, "Applies immediately"u8);
            var enabled = plugin.Configuration.Enabled;
            if (Gw.Toggle(ref enabled, "##enabled"u8))
            {
                plugin.Configuration.Enabled = enabled;
                plugin.Configuration.Save();
            }

            Gw.EndRow();

            Gw.BeginRow("Volume"u8);
            ImGui.SetNextItemWidth(170f * Frame.Scale);
            var volume = plugin.Configuration.Volume;
            if (Gw.Slider(ref volume, 0f, 1f, "##volume"u8, 0.01f, "0%"))
            {
                plugin.Configuration.Volume = volume;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (Gw.BeginSection("Actions"u8))
        {
            if (Gw.Button("Settings"u8))
            {
                plugin.ToggleConfigUi();
            }

            if (Gw.Button("Probe item sheet"u8, ButtonStyle.Prominent))
            {
                ProbeItemSheet();
            }
        }

        Gw.EndSection();

        // One save per frame at most, on the frame a value control finished an edit.
        if (Gw.Committed)
        {
            plugin.Configuration.Save();
        }

        ImGui.TextDisabled(Plugin.PluginInterface.ConfigDirectory.FullName);
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
