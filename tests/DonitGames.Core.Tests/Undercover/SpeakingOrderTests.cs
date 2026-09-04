using DonitGames.Core.Undercover;

namespace DonitGames.Core.Tests.Undercover;

public class SpeakingOrderTests
{
    private static UndercoverPlayer Player(UndercoverRole role) => new(Guid.NewGuid(), role, null, false, 0);

    [Fact]
    public void Mr_White_never_speaks_first_across_many_seeds()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var players = new[]
            {
                Player(UndercoverRole.Civilian),
                Player(UndercoverRole.Civilian),
                Player(UndercoverRole.Undercover),
                Player(UndercoverRole.MrWhite),
            };

            var order = SpeakingOrder.Compute(players, new Random(seed));
            var mrWhiteId = players.First(p => p.Role == UndercoverRole.MrWhite).SeatId;

            Assert.NotEqual(mrWhiteId, order[0]);
        }
    }

    [Fact]
    public void Mr_White_can_land_on_the_last_seat_eventually()
    {
        var landedLast = false;
        for (var seed = 0; seed < 500 && !landedLast; seed++)
        {
            var players = new[]
            {
                Player(UndercoverRole.Civilian),
                Player(UndercoverRole.Civilian),
                Player(UndercoverRole.MrWhite),
            };

            var order = SpeakingOrder.Compute(players, new Random(seed));
            var mrWhiteId = players.First(p => p.Role == UndercoverRole.MrWhite).SeatId;

            landedLast = order[^1] == mrWhiteId;
        }

        Assert.True(landedLast, "Mr. White never landed last across 500 seeds — insertion range looks off-by-one.");
    }

    [Fact]
    public void Every_active_player_appears_exactly_once()
    {
        var players = Enumerable.Range(0, 7)
            .Select(i => Player(i % 3 == 0 ? UndercoverRole.MrWhite : UndercoverRole.Civilian))
            .ToList();

        var order = SpeakingOrder.Compute(players, new Random(1));

        Assert.Equal(players.Select(p => p.SeatId).OrderBy(id => id), order.OrderBy(id => id));
    }

    [Fact]
    public void All_Mr_White_roster_returns_a_shuffled_order_without_crashing()
    {
        var players = new[] { Player(UndercoverRole.MrWhite), Player(UndercoverRole.MrWhite) };

        var order = SpeakingOrder.Compute(players, new Random(1));

        Assert.Equal(2, order.Count);
    }
}
