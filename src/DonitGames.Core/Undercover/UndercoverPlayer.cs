namespace DonitGames.Core.Undercover;

/// <summary>Per-seat game data. <see cref="Role"/>/<see cref="SecretWord"/> are null until that
/// seat has picked a card; <see cref="SecretWord"/> stays null forever for Mr. White.</summary>
public sealed record UndercoverPlayer(
    Guid SeatId,
    UndercoverRole? Role,
    string? SecretWord,
    bool IsEliminated,
    int Score);

/// <summary>The authoritative deck entry — carries the real role/word. Never sent to a client
/// as-is; <c>UndercoverSession.ViewFor</c> strips Role/Word down to just Index/TakenBySeatId.</summary>
public sealed record FaceDownCard(int Index, UndercoverRole Role, string? Word, Guid? TakenBySeatId);
