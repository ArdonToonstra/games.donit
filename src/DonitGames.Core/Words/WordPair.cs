namespace DonitGames.Core.Words;

/// <summary>An undrawn pair as it sits in the word list — no civilian/undercover role yet.</summary>
public sealed record WordPair(string WordA, string WordB);

/// <summary>
/// A pair after orientation has been chosen for one draw. <see cref="Source"/> is the original,
/// unoriented <see cref="WordPair"/> it came from — callers that track "already used in this
/// room" (draw-without-replacement) need it, since <c>Civilian</c>/<c>Undercover</c> alone can't
/// be compared back against <see cref="WordCategory.Pairs"/> once orientation has flipped.
/// </summary>
public sealed record OrientedPair(string Civilian, string Undercover, WordPair Source);

public sealed record WordCategory(
    string Name,
    string Description,
    string Language,
    IReadOnlyList<WordPair> Pairs);
