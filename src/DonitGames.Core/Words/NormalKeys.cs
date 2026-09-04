using System.Globalization;
using System.Text;

namespace DonitGames.Core.Words;

/// <summary>
/// Reduces a word to a case- and accent-insensitive key ("café" and "Cafe" collapse to the
/// same key; "kaas" and "kaa" do not — this only strips accents and whitespace, it never
/// collapses letters).
/// </summary>
public static class NormalKeys
{
    public static string Compute(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var decomposed = word.Trim().Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            stripped.Append(c);
        }

        return stripped.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
