namespace DonitGames.Core.Undercover;

public sealed record RoleCounts(int Civilians, int Undercover, int MrWhite)
{
    public int Total => Civilians + Undercover + MrWhite;
}

public static class RoleDistribution
{
    private static readonly Dictionary<int, RoleCounts> Table = new()
    {
        [3] = new RoleCounts(2, 1, 0),
        [4] = new RoleCounts(2, 1, 1),
        [5] = new RoleCounts(3, 1, 1),
        [6] = new RoleCounts(3, 2, 1),
        [7] = new RoleCounts(4, 2, 1),
        [8] = new RoleCounts(4, 2, 2),
        [9] = new RoleCounts(5, 2, 2),
        [10] = new RoleCounts(5, 3, 2),
    };

    public static RoleCounts Default(int playerCount)
    {
        if (!Table.TryGetValue(playerCount, out var counts))
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Undercover supports 3 to 10 players.");
        }

        return counts;
    }
}
