using DonitGames.Core.Rooms;

namespace DonitGames.Core.JustOne;

public sealed class JustOneSession : IGameSession<JustOneState, JustOneView>
{
    private static readonly HashSet<JustOnePhase> ClueTextVisibleToNonGuesser =
    [
        JustOnePhase.DuplicateReview,
        JustOnePhase.Guessing,
        JustOnePhase.JudgeReview,
        JustOnePhase.RoundResult,
    ];

    public JustOneView ViewFor(RoomSnapshot<JustOneState> snapshot, Guid seatId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var game = snapshot.Game;
        var judgeSeatId = JustOneEngine.Judge(snapshot);
        var isGuesser = game.GuesserSeatId == seatId;
        var isHost = snapshot.Seats.FirstOrDefault(s => s.SeatId == seatId)?.IsHost ?? false;

        var players = snapshot.Seats
            .Select(seat => new JustOnePlayerView(
                seat.SeatId,
                seat.DisplayName,
                seat.IsHost,
                snapshot.Presence.TryGetValue(seat.SeatId, out var presence) && presence.IsConnected,
                game.GuesserSeatId == seat.SeatId,
                judgeSeatId == seat.SeatId,
                game.Clues.Any(c => c.SeatId == seat.SeatId)))
            .ToList();

        var cancelled = game.AutoCancelledSeatIds
            .Concat(game.ReviewGroups.Where(g => g.ManuallyCancelled).SelectMany(g => g.SeatIds))
            .ToHashSet();
        var reviewFlagged = game.ReviewGroups.SelectMany(g => g.SeatIds).ToHashSet();

        // A cancelled clue is filtered OUT of the guesser's list entirely, not merely flagged —
        // "cancelled clues never appear in the guesser's view" is enforced by the type carrying
        // no such entry, the same projection discipline as Undercover's deck cards.
        IReadOnlyList<ClueView> clues = isGuesser
            ? (game.Phase is JustOnePhase.Guessing or JustOnePhase.JudgeReview or JustOnePhase.RoundResult
                ? game.Clues.Where(c => !cancelled.Contains(c.SeatId))
                    .Select(c => new ClueView(c.SeatId, c.Text, IsCancelled: false, IsFlaggedForReview: false))
                    .ToList()
                : [])
            : (ClueTextVisibleToNonGuesser.Contains(game.Phase)
                ? game.Clues.Select(c => new ClueView(c.SeatId, c.Text, cancelled.Contains(c.SeatId), reviewFlagged.Contains(c.SeatId))).ToList()
                : []);

        var secretWordVisible = !isGuesser || game.Phase == JustOnePhase.RoundResult;
        var guesserAttemptVisible = game.Phase is JustOnePhase.JudgeReview or JustOnePhase.RoundResult;

        IReadOnlyList<ReviewGroupView> reviewGroups = !isGuesser && ClueTextVisibleToNonGuesser.Contains(game.Phase)
            ? game.ReviewGroups.Select((g, i) => new ReviewGroupView(i, g.SeatIds, g.ManuallyCancelled)).ToList()
            : [];

        return new JustOneView(
            game.Phase,
            game.RoundNumber,
            players,
            isHost,
            isGuesser,
            judgeSeatId == seatId,
            secretWordVisible ? game.SecretWord : null,
            game.Clues.FirstOrDefault(c => c.SeatId == seatId)?.Text,
            clues,
            reviewGroups,
            guesserAttemptVisible ? game.GuesserAttempt : null,
            game.LastOutcome,
            game.PipsRemaining,
            game.CorrectCount,
            game.RoundsPlayed);
    }
}
