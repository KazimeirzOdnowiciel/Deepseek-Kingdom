namespace Sprintwatch.Services;

/// <summary>What the session stopwatch is doing right now.</summary>
internal enum SessionPhase
{
    /// <summary>Never started, or reset: nothing is accumulated and there are no laps.</summary>
    Idle,

    /// <summary>The monotonic clock is running, and laps can be recorded.</summary>
    Running,

    /// <summary>Stopped or paused: the elapsed time is frozen until <c>Start</c> resumes it.</summary>
    Paused,
}
