using System;
using Dalamud.Configuration;

namespace TerritoryCard;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Bump whenever a shape change needs a migration in <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; }

    /// <summary>How often the card may re-read the clock, in milliseconds. Range 250-5000.</summary>
    public int RefreshIntervalMs { get; set; } = 1000;

    /// <summary>When false the clock is drawn as 12-hour with an AM/PM suffix.</summary>
    public bool Use24HourClock { get; set; } = true;

    /// <summary>Open the card on login, once a cutscene is no longer playing.</summary>
    public bool OpenOnLogin { get; set; }

    /// <summary>Persisted so a reopened window comes back where the user left it.</summary>
    public bool MainWindowOpen { get; set; }

    /// <summary>
    /// Runs once after load. Never delete a field here: assign defaults for the new shape
    /// instead, because the game user's existing JSON on disk is not inspectable.
    /// </summary>
    public void Migrate()
    {
        // A hand-edited JSON can hold a value outside the range even at the current version, so
        // the clamp runs on every load and not only on a version bump.
        RefreshIntervalMs = RefreshIntervalMs <= 0 ? 1000 : Math.Clamp(RefreshIntervalMs, 250, 5000);

        if (Version >= CurrentVersion)
        {
            return;
        }

        Version = CurrentVersion;
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
