using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glasswork;
using Glasswork.Dalamud;
using TerritoryCard.Services;
using TerritoryCard.Windows;

namespace TerritoryCard;

/// <summary>
/// Entry point. Services are injected by Dalamud into the <see cref="PluginServiceAttribute"/>
/// properties before the constructor runs, which is why they are never null here.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public static string DisplayName => "Territory Card";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [PluginService] internal static IDataManager Data { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>
    /// The InternalName is derived from AssemblyName and must never change after release:
    /// it is the config directory, the log tag and the plugin identity.
    /// </summary>
    private const string CommandName = "/territorycard";

    private readonly LoginAutoOpen autoOpen = new();

    private bool loggedConditionFailure;

    public Configuration Configuration { get; }

    /// <summary>One Glasswork host per plugin: it owns the glass layer and the theme.</summary>
    public GlassworkHost Host { get; }

    public readonly WindowSystem WindowSystem = new("TerritoryCard");

    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

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
                HelpMessage = "Toggles the territory card.\n"
                              + $"{CommandName} config - Opens the settings window.\n"
                              + $"{CommandName} status - Prints the territory and Eorzea time to chat.",
            });

        // Handlers only set flags. The sheet is read later, in Draw, on the UI thread: a Lumina
        // read from an event handler races sheet initialisation during a zone change.
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        ClientState.TerritoryChanged += OnTerritoryChanged;

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

        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        ClientState.TerritoryChanged -= OnTerritoryChanged;

        autoOpen.Cancel();

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
    private void OnDraw()
    {
        Host.BeginFrame();
        OpenCardIfRequested();
        WindowSystem.Draw();
        Host.EndFrame();
    }

    /// <summary>
    /// Applies a deferred open requested by <see cref="OnLogin"/>. It never opens over a cutscene,
    /// never while logged out, and gives up after the state machine's deadline rather than
    /// surprising the player in a duty later.
    /// </summary>
    private void OpenCardIfRequested()
    {
        if (!autoOpen.IsPending)
        {
            return;
        }

        // Short-circuit on purpose: no condition read is needed while logged out.
        var loggedIn = ClientState.IsLoggedIn;
        var inCutscene = loggedIn && InCutscene();

        if (autoOpen.ShouldOpen(loggedIn, inCutscene, Environment.TickCount64))
        {
            mainWindow.IsOpen = true;
        }
        else if (!autoOpen.IsPending)
        {
            // ShouldOpen drops the request without opening only when the deadline passed. Giving up
            // is the point - a card that appears minutes later, mid-fight, is worse than no card -
            // but it is worth one Debug line rather than silence.
            Log.Debug("Auto-open gave up: the deadline passed before a login outside a cutscene.");
        }
    }

    /// <summary>
    /// Whether a cutscene is playing. A failed condition lookup counts as "in a cutscene" so the
    /// plugin fails closed: a missing auto-open is harmless, a card over a cinematic is not.
    /// </summary>
    private bool InCutscene()
    {
        try
        {
            var inCutscene = Condition.Any(
                ConditionFlag.WatchingCutscene,
                ConditionFlag.WatchingCutscene78,
                ConditionFlag.OccupiedInCutSceneEvent);

            if (loggedConditionFailure)
            {
                loggedConditionFailure = false;
                Log.Debug("Condition lookup recovered; auto-open is no longer treated as blocked.");
            }

            return inCutscene;
        }
        catch (Exception ex)
        {
            // Log the transition into failure, not every frame of it: a broken condition service
            // must not spam the log at 60 lines a second.
            if (!loggedConditionFailure)
            {
                loggedConditionFailure = true;
                Log.Debug(ex, "Condition lookup failed; treating the player as being in a cutscene.");
            }

            return true;
        }
    }

    private void OnLogin()
    {
        if (Configuration.OpenOnLogin)
        {
            autoOpen.Request(Environment.TickCount64);
        }
    }

    private void OnLogout(int type, int code)
    {
        autoOpen.Cancel();
        mainWindow.InvalidateTerritory();
    }

    private void OnTerritoryChanged(uint territoryId) => mainWindow.InvalidateTerritory();

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                mainWindow.Toggle();
                break;

            case "config":
                ToggleConfigUi();
                break;

            case "status":
                ChatGui.Print(mainWindow.DescribeStatus(), DisplayName);
                break;

            default:
                ChatGui.PrintError(
                    $"unknown subcommand '{args.Trim()}'. Try {CommandName} config or {CommandName} status.",
                    DisplayName);
                break;
        }
    }
}
