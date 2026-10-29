using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork.Backend;

namespace Sprintwatch.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private const float Radius = 20f;

    /// <summary>
    /// Labels for <c>Gw.Segmented</c>, in index order: 0 is 12-hour, 1 is 24-hour. A static array,
    /// never a collection expression in the draw path, because the widget re-reads it every frame.
    /// </summary>
    private static readonly string[] ClockFormats = { "12-hour", "24-hour" };

    private readonly Plugin plugin;

    /// <summary>Set once a draw has failed, so the failure is logged on the transition (N4).</summary>
    private bool loggedDrawFailure;

    public ConfigWindow(Plugin plugin)
        : base(
            "Sprint Watch Settings###SprintwatchConfig",
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(340, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Radius * plugin.Host.Scale);
        base.PreDraw();
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        base.PostDraw();
    }

    public override void Draw()
    {
        try
        {
            DrawBody();

            if (loggedDrawFailure)
            {
                Plugin.Log.Debug("ConfigWindow.Draw recovered.");
                loggedDrawFailure = false;
            }
        }
        catch (Exception ex)
        {
            // Same reason as MainWindow: a per-frame catch, so log on the transition only (N4).
            if (!loggedDrawFailure)
            {
                Plugin.Log.Error(ex, "ConfigWindow.Draw failed.");
                loggedDrawFailure = true;
            }
        }
    }

    /// <summary>
    /// Configuration mutates in place and saves once at the end of the frame, so a toggle does not
    /// write the config file more than once.
    /// </summary>
    private void DrawBody()
    {
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        var config = plugin.Configuration;
        var changed = false;

        if (Gw.BeginSection("Laps"u8, "Hiding the table keeps the timer compact in a duty."u8))
        {
            Gw.BeginRow("Show laps"u8);

            var showLaps = config.ShowLaps;

            if (Gw.Toggle(ref showLaps, "##showLaps"u8))
            {
                config.ShowLaps = showLaps;
                changed = true;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (Gw.BeginSection("Clock"u8, "Lap stamps use the local wall clock."u8))
        {
            Gw.BeginRow("Hour format"u8);

            var style = config.Use24HourClock ? 1 : 0;

            if (Gw.Segmented(ref style, ClockFormats))
            {
                config.Use24HourClock = style == 1;
                changed = true;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (changed)
        {
            config.Save();
        }
    }
}
