using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Sprintwatch.Services;
using Sprintwatch.Views;

namespace Sprintwatch.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const float Radius = 22f;

    private readonly Plugin plugin;

    /// <summary>Set once a draw has failed, so the failure is logged on the transition (N4).</summary>
    private bool loggedDrawFailure;

    /// <summary>
    /// <c>NoBackground</c> stops ImGui painting its own fill: the glass is the background.
    /// <c>NoFocusOnAppearing</c> and <c>NoNavFocus</c> keep the window from taking keyboard focus
    /// when it opens mid-duty. The <c>###</c> suffix is the stable ImGui id, so renaming the visible
    /// title never resets a user's window position.
    /// </summary>
    public MainWindow(Plugin plugin)
        : base(
            "Sprint Watch###SprintwatchMain",
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
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
        Plugin.SaveConfigSafe(plugin.Configuration);
        base.OnOpen();
    }

    public override void OnClose()
    {
        plugin.Configuration.MainWindowOpen = false;
        Plugin.SaveConfigSafe(plugin.Configuration);
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

            if (loggedDrawFailure)
            {
                Plugin.Log.Debug("MainWindow.Draw recovered.");
                loggedDrawFailure = false;
            }
        }
        catch (Exception ex)
        {
            // While a failure persists this runs every frame, and the catch also swallows the
            // exception, so Plugin.OnDraw's transition guard never sees it. The flag has to live
            // here or this is a 60 Hz Error stream (N4).
            if (!loggedDrawFailure)
            {
                Plugin.Log.Error(ex, "MainWindow.Draw failed.");
                loggedDrawFailure = true;
            }
        }
    }

    /// <summary>
    /// Builds the readout from the timer and the config, hands it to the view with the commands
    /// that mutate them, and lets the view decide what a click means. The view never touches a
    /// service, the config or the wall clock, which is what keeps it previewable (T7).
    /// </summary>
    private void DrawBody()
    {
        var config = plugin.Configuration;

        // Records one glass callback that refracts whatever is behind the window - the game
        // and other plugins' windows included. Falls back to a flat fill when the device is
        // not ready or the GPU budget is spent, and never throws.
        plugin.Host.DrawWindowBackground(Radius, 0.5f, plugin.Host.Look.IsLight);

        var timer = plugin.Timer;

        var readout = SessionReadoutBuilder.Build(
            timer,
            config.ShowLaps,
            config.Use24HourClock ? HourFormat.TwentyFourHour : HourFormat.TwelveHour,
            TimeZoneInfo.Local);

        var commands = new ViewCommands(
            Start: timer.Start,
            Stop: timer.Stop,
            Reset: timer.Reset,
            Lap: AddLap,
            SetShowLaps: SetShowLaps,
            SetHourFormat: SetHourFormat);

        SprintwatchView.Draw(readout, commands);
    }

    /// <summary>
    /// The view disables its lap control from <see cref="SessionReadout.CanLap"/>, so a rejected
    /// lap here is a command racing the frame, not a user error; the next readout reflects it.
    /// </summary>
    private void AddLap() => _ = plugin.Timer.TryAddLap();

    /// <summary>
    /// One config write per change, not one per frame: the view reports a toggle once and the
    /// value is otherwise already what the config holds.
    /// </summary>
    private void SetShowLaps(bool showLaps)
    {
        if (plugin.Configuration.ShowLaps == showLaps)
        {
            return;
        }

        plugin.Configuration.ShowLaps = showLaps;
        plugin.Configuration.Save();
    }

    /// <summary>See <see cref="SetShowLaps"/>: one save per format change.</summary>
    private void SetHourFormat(HourFormat format)
    {
        var use24HourClock = format == HourFormat.TwentyFourHour;

        if (plugin.Configuration.Use24HourClock == use24HourClock)
        {
            return;
        }

        plugin.Configuration.Use24HourClock = use24HourClock;
        plugin.Configuration.Save();
    }
}
