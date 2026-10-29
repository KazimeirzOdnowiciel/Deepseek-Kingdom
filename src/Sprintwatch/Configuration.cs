using System;
using Dalamud.Configuration;

namespace Sprintwatch;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Bump whenever a shape change needs a migration in <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; }

    /// <summary>Draw the lap table under the timer. Hiding it keeps the window compact in a duty.</summary>
    public bool ShowLaps { get; set; } = true;

    /// <summary>When false the lap stamps are drawn as a 12-hour clock with AM/PM.</summary>
    public bool Use24HourClock { get; set; } = true;

    /// <summary>Persisted so a reopened window comes back where the user left it.</summary>
    public bool MainWindowOpen { get; set; }

    /// <summary>
    /// Runs once after load. Never delete a field here: assign defaults for the new shape instead,
    /// because the game user's existing JSON on disk is not inspectable.
    /// </summary>
    public void Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return;
        }

        // Version 0 is the scaffold, which had neither a lap panel nor a clock preference. A fresh
        // install gets the same defaults from the property initialisers, so this only fills in a
        // config that was written before those fields existed.
        ShowLaps = true;
        Use24HourClock = true;

        Version = CurrentVersion;
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
