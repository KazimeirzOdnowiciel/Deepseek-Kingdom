using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork.Backend;

namespace TerritoryCard.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private const float Radius = 20f;

    /// <summary>
    /// Labels for <c>Gw.Segmented</c>. A static array, never a collection expression in the draw
    /// path: the widget keeps its selection by index and the labels are read every frame.
    /// </summary>
    private static readonly string[] ClockStyles = { "12-hour", "24-hour" };

    private readonly Plugin plugin;

    private bool loggedDrawFailure;

    public ConfigWindow(Plugin plugin)
        : base("Territory Card Settings###TerritoryCardConfig", ImGuiWindowFlags.NoBackground)
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
            loggedDrawFailure = false;
        }
        catch (Exception ex)
        {
            // Log the transition into failure at Error and later frames at Debug: a Draw that throws
            // every frame must not write sixty stack traces a second during a duty (N4).
            if (loggedDrawFailure)
            {
                Plugin.Log.Debug(ex, "ConfigWindow.Draw is still failing.");
            }
            else
            {
                loggedDrawFailure = true;
                Plugin.Log.Error(ex, "ConfigWindow.Draw failed.");
            }
        }
    }

    /// <summary>
    /// Configuration mutates in place and saves once at the end of the frame, so dragging a
    /// slider does not write the config file sixty times a second.
    /// </summary>
    private void DrawBody()
    {
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        var config = plugin.Configuration;
        var changed = false;

        if (Gw.BeginSection("Clock"u8, "The clock is redrawn at most this often; the ET minute changes every ~2.9 s."u8))
        {
            Gw.BeginRow("Format"u8);

            var style = config.Use24HourClock ? 1 : 0;

            if (Gw.Segmented(ref style, ClockStyles))
            {
                config.Use24HourClock = style == 1;
                changed = true;
            }

            Gw.EndRow();

            Gw.BeginRow("Refresh interval"u8);
            ImGui.SetNextItemWidth(170f * Frame.Scale);

            var interval = config.RefreshIntervalMs;

            if (Gw.Slider(ref interval, 250, 5000, "##refreshInterval"u8, 250, "%d ms"))
            {
                config.RefreshIntervalMs = interval;
                changed = true;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (Gw.BeginSection("Window"u8, "Waits for a cutscene to end before opening."u8))
        {
            Gw.BeginRow("Open on login"u8);

            var openOnLogin = config.OpenOnLogin;

            if (Gw.Toggle(ref openOnLogin, "##openOnLogin"u8))
            {
                config.OpenOnLogin = openOnLogin;
                changed = true;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (changed || Gw.Committed)
        {
            config.Save();
        }
    }
}
