using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glasswork;
using Glasswork.Dalamud;
using PluginName.Windows;

namespace PluginName;

/// <summary>
/// Entry point. Services are injected by Dalamud into the <see cref="PluginServiceAttribute"/>
/// properties before the constructor runs, which is why they are never null here.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public static string DisplayName => "Plugin Name";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [PluginService] internal static IDataManager Data { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>
    /// The InternalName is derived from AssemblyName and must never change after release:
    /// it is the config directory, the log tag and the plugin identity.
    /// </summary>
    private const string CommandName = "/newplugin";

    public Configuration Configuration { get; }

    /// <summary>One Glasswork host per plugin: it owns the glass layer and the theme.</summary>
    public GlassworkHost Host { get; }

    public readonly WindowSystem WindowSystem = new("PluginName");

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    /// <summary>Set once the draw pass has failed, so it is logged on the transition and not per frame.</summary>
    private bool loggedDrawFailure;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

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
                HelpMessage = "Opens the main window.\n"
                              + $"{CommandName} config - Opens settings.\n"
                              + $"{CommandName} status - Prints current state to chat.",
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

            case "config":
                configWindow.Toggle();
                break;

            case "status":
                ChatGui.Print($"[{DisplayName}] {Configuration.Enabled switch { true => "enabled", false => "disabled" }}, volume {Configuration.Volume:P0}.");
                break;

            default:
                ChatGui.PrintError($"[{DisplayName}] unknown subcommand '{args.Trim()}'. Try {CommandName} config or {CommandName} status.");
                break;
        }
    }
}
