namespace DonitGames.Core.JustOne;

public sealed record Clue(Guid SeatId, string Text);

/// <summary>A connected component of near-duplicate clues (Phase 1's
/// <c>WordNormalizer.IsNearDuplicate</c>) that contains no exact match — flagged for the table
/// to decide about, never auto-cancelled, since a false cancel destroys information the table
/// can't recover.</summary>
public sealed record ReviewGroup(IReadOnlyList<Guid> SeatIds, bool ManuallyCancelled);
