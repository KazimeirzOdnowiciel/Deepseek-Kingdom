using System;
using System.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glasswork;
using Glasswork.Dalamud;
using Sprintwatch.Services;
using Sprintwatch.Windows;

namespace Sprintwatch;

/// <summary>
/// Entry point. Services are injected by Dalamud into the <see cref="PluginServiceAttribute"/>
/// properties before the constructor runs, which is why they are never null here.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public static string DisplayName => "Sprint Watch";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>
    /// The InternalName is derived from AssemblyName and must never change after release:
    /// it is the config directory, the log tag and the plugin identity.
    /// </summary>
    private const string CommandName = "/sprintwatch";

    public Configuration Configuration { get; }

    /// <summary>One Glasswork host per plugin: it owns the glass layer and the theme.</summary>
    public GlassworkHost Host { get; }

    public readonly WindowSystem WindowSystem = new("Sprintwatch");

    /// <summary>
    /// The session the windows draw and the subcommands drive. Elapsed is a difference of monotonic
    /// reads, never a per-frame accumulation; the wall clock is read only to stamp a lap.
    /// </summary>
    internal SessionStopwatch Timer { get; }

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    /// <summary>Set once the draw pass has failed, so it is logged on the transition and not per frame.</summary>
    private bool loggedDrawFailure;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

        // Stopwatch.GetTimestamp is monotonic; GetElapsedTime subtracts a baseline from it, so one
        // baseline read here turns the pair into the stopwatch's monotonic clock. DateTimeOffset.UtcNow
        // is never used for elapsed: a system clock correction must not move a running sprint.
        var monotonicOrigin = Stopwatch.GetTimestamp();

        Timer = new SessionStopwatch(
            () => Stopwatch.GetElapsedTime(monotonicOrigin),
            () => DateTimeOffset.UtcNow);

        Host = new GlassworkHost(PluginInterface, Log);

        // One assignment restyles every widget, surface and pane. Other families: Neutral,
        // Liquid, Minimal, HighContrast, Retro, Classic, Clear Blue/Grey/Green/Pink, Aurora,
        // Catppuccin, Nord - each also exists in a Light variant.
        Host.Look = Look.Eorzea();

        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                // Enumerate every subcommand. A bare "opens the window" leaves the user
                // guessing at what else exists.
                HelpMessage = "Opens the sprint timer.\n"
                              + $"{CommandName} start - Starts the timer, or resumes it after a stop.\n"
                              + $"{CommandName} stop - Pauses the timer and freezes the elapsed time.\n"
                              + $"{CommandName} lap - Records a lap split while the timer is running.\n"
                              + $"{CommandName} reset - Clears the elapsed time and every lap.\n"
                              + $"{CommandName} config - Opens settings.\n"
                              + $"{CommandName} status - Prints the timer state to chat.",
            });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("{Plugin} loaded.", DisplayName);
    }

    public void Dispose()
    {
        // Unhook everything: a leaked handler survives an in-game plugin reload and
        // then draws into a disposed window system.
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        // Last, and only after Draw is unsubscribed: a glass callback recorded after the
        // layer is gone faults the client.
        Host.Dispose();
    }

    public void ToggleConfigUi() => configWindow.Toggle();

    public void ToggleMainUi() => mainWindow.Toggle();

    /// <summary>
    /// BeginFrame first, EndFrame last, around every window. BeginFrame releases the previous
    /// frame's captures and picks up the device, the UI scale and the reduced-motion setting.
    /// </summary>
    /// <remarks>
    /// EndFrame is in a finally on purpose: if a window throws, the glass frame this opened must
    /// still be closed (N11). Logging happens on the transition only - this path runs sixty times a
    /// second, so a per-frame Error or Debug line becomes log spam.
    /// </remarks>
    private void OnDraw()
    {
        Host.BeginFrame();

        try
        {
            WindowSystem.Draw();

            if (loggedDrawFailure)
            {
                Log.Debug("The UI draw pass recovered.");
                loggedDrawFailure = false;
            }
        }
        catch (Exception ex)
        {
            if (!loggedDrawFailure)
            {
                Log.Error(ex, "The UI draw pass failed.");
                loggedDrawFailure = true;
            }
        }
        finally
        {
            Host.EndFrame();
        }
    }

    /// <summary>
    /// Saves the configuration without letting a failed write escape.
    /// </summary>
    /// <remarks>
    /// <c>Window.OnOpen</c> and <c>OnClose</c> are invoked from <c>WindowSystem.Draw</c>, outside the
    /// guard around <c>Window.Draw</c>, so an exception during a save there aborts the whole draw
    /// pass and skips <c>Host.EndFrame</c>.
    /// </remarks>
    internal static void SaveConfigSafe(Configuration configuration)
    {
        try
        {
            configuration.Save();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Saving the configuration failed.");
        }
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                mainWindow.Toggle();
                break;

            case "start":
                StartTimer();
                break;

            case "stop":
                StopTimer();
                break;

            case "lap":
                AddLap();
                break;

            case "reset":
                Timer.Reset();
                ChatGui.Print($"[{DisplayName}] Timer and laps cleared.");
                break;

            case "config":
                configWindow.Toggle();
                break;

            case "status":
                PrintStatus();
                break;

            default:
                ChatGui.PrintError(
                    $"[{DisplayName}] Unknown subcommand '{args.Trim()}'. Try {CommandName} start, stop, lap, reset, config or status.");
                break;
        }
    }

    private void StartTimer()
    {
        if (Timer.Phase == SessionPhase.Running)
        {
            ChatGui.Print($"[{DisplayName}] The timer is already running.");
            return;
        }

        Timer.Start();
        ChatGui.Print($"[{DisplayName}] Timer started.");
    }

    private void StopTimer()
    {
        if (Timer.Phase != SessionPhase.Running)
        {
            ChatGui.Print($"[{DisplayName}] The timer is not running.");
            return;
        }

        Timer.Stop();
        ChatGui.Print($"[{DisplayName}] Timer paused at {DurationFormat.Elapsed(Timer.Elapsed)}.");
    }

    private void AddLap()
    {
        if (!Timer.TryAddLap())
        {
            ChatGui.PrintError($"[{DisplayName}] A lap can only be recorded while the timer is running.");
            return;
        }

        var lap = Timer.Laps[Timer.Laps.Count - 1];
        ChatGui.Print(
            $"[{DisplayName}] Lap split {DurationFormat.Elapsed(lap.Split)}, total {DurationFormat.Elapsed(lap.Total)}.");
    }

    private void PrintStatus()
    {
        var phase = Timer.Phase switch
        {
            SessionPhase.Running => "running",
            SessionPhase.Paused => "paused",
            _ => "idle",
        };

        ChatGui.Print(
            $"[{DisplayName}] {phase}, {DurationFormat.Elapsed(Timer.Elapsed)}, {Timer.Laps.Count} lap(s) recorded.");
    }
}
