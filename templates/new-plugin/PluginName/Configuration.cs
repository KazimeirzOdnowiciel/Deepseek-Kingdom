using System;
using Dalamud.Configuration;

namespace PluginName;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Bump whenever a shape change needs a migration in <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; }

    public bool Enabled { get; set; } = true;

    public float Volume { get; set; } = 0.6f;

    public int MinimumGapMs { get; set; } = 250;

    /// <summary>Persisted so a reopened window comes back where the user left it.</summary>
    public bool MainWindowOpen { get; set; }

    /// <summary>
    /// Runs once after load. Never delete fields here: assign defaults for the new
    /// shape instead, because the game user's existing JSON on disk is not inspectable.
    /// </summary>
    public void Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return;
        }

        Volume = Math.Clamp(Volume, 0f, 1f);
        MinimumGapMs = Math.Clamp(MinimumGapMs, 0, 10_000);

        Version = CurrentVersion;
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
