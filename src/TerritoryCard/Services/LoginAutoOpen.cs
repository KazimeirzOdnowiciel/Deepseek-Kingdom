namespace TerritoryCard.Services;

/// <summary>
/// The deferred auto-open requested by the <c>Login</c> event: it may only fire when a character is
/// logged in and no cutscene is playing, and it gives up after a deadline so a card never pops up
/// over a cinematic or a fight. No Dalamud dependency, so every transition is unit tested.
/// </summary>
internal sealed class LoginAutoOpen
{
    private readonly long deadlineMs;

    private bool pending;
    private long requestedAtMs;

    public LoginAutoOpen(int deadlineMs = 120_000)
    {
        this.deadlineMs = deadlineMs < 0 ? 0 : deadlineMs;
    }

    /// <summary>Whether an open is still waiting for a safe moment.</summary>
    public bool IsPending => pending;

    /// <summary>Asks for an open and restarts the deadline from <paramref name="nowMs"/>.</summary>
    public void Request(long nowMs)
    {
        pending = true;
        requestedAtMs = nowMs;
    }

    /// <summary>
    /// Returns true exactly once per <see cref="Request"/> when it is safe to open: logged in, not in
    /// a cutscene, and not past the deadline. Every other call returns false and leaves the request
    /// pending, except when the deadline has passed - then it is dropped for good.
    /// </summary>
    /// <remarks>
    /// The deadline is checked before the logged-in and cutscene states on purpose: a request that
    /// waited too long is abandoned rather than fired late, which is what keeps a slow login or a
    /// long cutscene from opening the card at an arbitrary moment later.
    /// </remarks>
    public bool ShouldOpen(bool loggedIn, bool inCutscene, long nowMs)
    {
        if (!pending)
        {
            return false;
        }

        if (nowMs - requestedAtMs > deadlineMs)
        {
            pending = false;
            return false;
        }

        if (!loggedIn || inCutscene)
        {
            return false;
        }

        pending = false;
        return true;
    }

    /// <summary>Drops a pending request, e.g. on logout.</summary>
    public void Cancel() => pending = false;
}
