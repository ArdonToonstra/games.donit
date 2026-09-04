using DonitGames.Core.Rooms;

namespace DonitGames.Core.Undercover;

public sealed class UndercoverSession : IGameSession<UndercoverState, UndercoverView>
{
    public UndercoverView ViewFor(RoomSnapshot<UndercoverState> snapshot, Guid seatId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var game = snapshot.Game;
        var viewer = game.Players.FirstOrDefault(p => p.SeatId == seatId);
        var viewerSeat = snapshot.Seats.FirstOrDefault(s => s.SeatId == seatId);

        var players = game.Players
            .Select(p =>
            {
                var seat = snapshot.Seats.FirstOrDefault(s => s.SeatId == p.SeatId);
                // Once the game is over everyone's role is fair game — that's the whole point of
                // a results screen. Before that: eliminated seats are revealed, and you always
                // see your own.
                var revealToViewer = p.IsEliminated || p.SeatId == seatId || game.Phase == UndercoverPhase.Results;
                return new UndercoverPlayerView(
                    p.SeatId,
                    seat?.DisplayName ?? "",
                    seat?.IsHost ?? false,
                    snapshot.Presence.TryGetValue(p.SeatId, out var presence) && presence.IsConnected,
                    p.IsEliminated,
                    revealToViewer ? p.Role : null,
                    revealToViewer ? p.SecretWord : null,
                    p.Score);
            })
            .ToList();

        var deck = game.Deck
            .Select(c => new UndercoverCardView(c.Index, c.TakenBySeatId))
            .ToList();

        var showVoteBreakdown = game.Votes.Count > 0 &&
            (game.Phase == UndercoverPhase.EliminationReveal || game.TiedSeatIds.Count > 0);

        return new UndercoverView(
            game.Phase,
            game.RoundNumber,
            players,
            deck,
            game.SpeakingOrder,
            game.DiscussionDeadlineUtc,
            viewerSeat?.IsHost ?? false,
            viewer?.Role,
            viewer?.SecretWord,
            game.Votes.ContainsKey(seatId),
            showVoteBreakdown ? game.Votes : null,
            game.TiedSeatIds,
            game.JustEliminatedSeatId,
            game.MrWhiteGuesserSeatId == seatId,
            game.Winner);
    }
}
