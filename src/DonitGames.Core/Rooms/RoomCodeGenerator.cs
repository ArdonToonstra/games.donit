namespace DonitGames.Core.Rooms;

/// <summary>
/// Alphabet excludes 0/O, 1/I/L, 5/S, 2/Z, 8/B — characters that get misheard when a room code
/// is read aloud across a table.
/// </summary>
public static class RoomCodeGenerator
{
    public const string Alphabet = "ACDEFGHJKMNPQRTUVWXY34679";
    public const int Length = 4;

    public static string Generate(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        Span<char> buffer = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[rng.Next(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
