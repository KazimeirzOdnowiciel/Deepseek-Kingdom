using TerritoryCard.Services;
using Xunit;

namespace TerritoryCard.Tests;

/// <summary>
/// Tests cover logic that has no Dalamud dependency. Everything that reads the game or
/// draws ImGui cannot run outside the client, so it is verified in-game instead; see
/// CONTRIBUTING.md, "Testing".
/// </summary>
public sealed class ThrottleTests
{
    [Fact]
    public void FirstCallPasses()
    {
        var throttle = new Throttle(250);

        Assert.True(throttle.TryPass(1_000));
    }

    [Fact]
    public void RejectsInsideTheGap()
    {
        var throttle = new Throttle(250);
        throttle.TryPass(1_000);

        Assert.False(throttle.TryPass(1_249));
    }

    [Fact]
    public void AcceptsExactlyAtTheGap()
    {
        var throttle = new Throttle(250);
        throttle.TryPass(1_000);

        Assert.True(throttle.TryPass(1_250));
    }

    [Fact]
    public void ResetAllowsAnImmediatePass()
    {
        var throttle = new Throttle(250);
        throttle.TryPass(1_000);

        throttle.Reset();

        Assert.True(throttle.TryPass(1_001));
    }

    [Fact]
    public void ZeroGapPassesEveryTime()
    {
        var throttle = new Throttle(0);
        throttle.TryPass(1_000);

        Assert.True(throttle.TryPass(1_000));
    }

    [Fact]
    public void NegativeGapIsTreatedAsZero()
    {
        var throttle = new Throttle(-500);
        throttle.TryPass(1_000);

        Assert.True(throttle.TryPass(1_000));
    }
}
