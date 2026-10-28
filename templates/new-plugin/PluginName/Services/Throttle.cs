namespace PluginName.Services;

/// <summary>
/// A minimum-interval rate limiter with no Dalamud dependency, so it can be unit tested.
/// Keep logic like this out of the window classes: anything that touches ImGui or the
/// game cannot be exercised outside the client.
/// </summary>
internal sealed class Throttle
{
    private readonly long minimumGapMs;

    private long? lastPassMs;

    public Throttle(long minimumGapMs)
    {
        this.minimumGapMs = minimumGapMs < 0 ? 0 : minimumGapMs;
    }

    /// <summary>Returns true and records <paramref name="nowMs"/> if the interval has elapsed.</summary>
    public bool TryPass(long nowMs)
    {
        if (lastPassMs is { } last && nowMs - last < minimumGapMs)
        {
            return false;
        }

        lastPassMs = nowMs;
        return true;
    }

    public void Reset() => lastPassMs = null;
}
