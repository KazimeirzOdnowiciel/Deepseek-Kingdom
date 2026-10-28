using TerritoryCard.Services;
using Xunit;

namespace TerritoryCard.Tests;

public sealed class TerritoryLabelTests
{
    [Fact]
    public void KeepsTheSheetName()
        => Assert.Equal("Central Shroud", TerritoryLabel.Resolve("Central Shroud", 148));

    [Fact]
    public void KeepsApostrophesAndLeadingArticles()
        => Assert.Equal("The Wanderer's Palace", TerritoryLabel.Resolve("The Wanderer's Palace", 188));

    [Fact]
    public void TrimsSurroundingWhitespace()
        => Assert.Equal("Sastasha", TerritoryLabel.Resolve("  Sastasha\t", 1036));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPlaceNameFallsBackToTheRowId(string? placeName)
        => Assert.Equal("#1036", TerritoryLabel.Resolve(placeName, 1036));

    [Fact]
    public void TheFallbackIdIsCultureInvariant()
        => Assert.Equal("#148", TerritoryLabel.Resolve(null, 148));

    [Fact]
    public void LoggedOutTextIsTheOneTheWindowDraws()
        => Assert.Equal("Not logged in", TerritoryLabel.LoggedOut);
}
