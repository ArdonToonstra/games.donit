using DonitGames.Core.Undercover;

namespace DonitGames.Core.Tests.Undercover;

public class RoleDistributionTests
{
    [Theory]
    [InlineData(3, 2, 1, 0)]
    [InlineData(4, 2, 1, 1)]
    [InlineData(5, 3, 1, 1)]
    [InlineData(6, 3, 2, 1)]
    [InlineData(7, 4, 2, 1)]
    [InlineData(8, 4, 2, 2)]
    [InlineData(9, 5, 2, 2)]
    [InlineData(10, 5, 3, 2)]
    public void Default_matches_the_documented_table(int playerCount, int civilians, int undercover, int mrWhite)
    {
        var counts = RoleDistribution.Default(playerCount);

        Assert.Equal(civilians, counts.Civilians);
        Assert.Equal(undercover, counts.Undercover);
        Assert.Equal(mrWhite, counts.MrWhite);
        Assert.Equal(playerCount, counts.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(11)]
    [InlineData(20)]
    public void Default_throws_outside_the_supported_range(int playerCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoleDistribution.Default(playerCount));
    }
}
