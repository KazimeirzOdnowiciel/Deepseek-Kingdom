using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glasswork.Backend;

namespace PluginName.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private const float Radius = 20f;

    private readonly Plugin plugin;

    /// <summary>Set once a draw has failed, so the failure is logged on the transition (N4).</summary>
    private bool loggedDrawFailure;

    public ConfigWindow(Plugin plugin)
        : base("Plugin Name Settings###PluginNameConfig", ImGuiWindowFlags.NoBackground)
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
    /// Configuration mutates in place and saves once at the end of the frame, so a slider
    /// drag does not write the config file sixty times a second.
    /// </summary>
    private void DrawBody()
    {
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        var config = plugin.Configuration;
        var changed = false;

        if (Gw.BeginSection("Audio"u8, "Applies to alert sounds only."u8))
        {
            Gw.BeginRow("Volume"u8);
            ImGui.SetNextItemWidth(170f * Frame.Scale);
            var volume = config.Volume;
            if (Gw.Slider(ref volume, 0f, 1f, "##volume"u8, 0.01f, "0%"))
            {
                config.Volume = volume;
                changed = true;
            }

            Gw.EndRow();

            Gw.BeginRow("Minimum gap"u8, "Milliseconds between alerts."u8);
            ImGui.SetNextItemWidth(170f * Frame.Scale);
            var gap = config.MinimumGapMs;
            if (Gw.Slider(ref gap, 0, 10_000, "##gap"u8, 50, "%d ms"))
            {
                config.MinimumGapMs = gap;
                changed = true;
            }

            Gw.EndRow();
        }

        Gw.EndSection();

        if (Gw.BeginSection("Behaviour"u8))
        {
            Gw.BeginRow("Enabled"u8, "Turning this off silences every trigger."u8);
            var enabled = config.Enabled;
            if (Gw.Toggle(ref enabled, "##enabled"u8))
            {
                config.Enabled = enabled;
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
