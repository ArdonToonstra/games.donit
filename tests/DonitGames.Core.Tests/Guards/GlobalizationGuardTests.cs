using System.Globalization;
using System.Text;

namespace DonitGames.Core.Tests.Guards;

/// <summary>
/// Guards the two build settings that quietly break this app if they ever flip.
/// See CLAUDE.md, non-negotiable #7.
/// </summary>
public class GlobalizationGuardTests
{
    [Fact]
    public void UnicodeDecompositionWorks()
    {
        // This is the core of the Just One duplicate normaliser: decompose, then drop
        // non-spacing marks so "café" and "cafe" collide. In globalization-invariant
        // mode Normalize throws PlatformNotSupportedException, and duplicate detection
        // silently stops folding accents.
        var decomposed = "café".Normalize(NormalizationForm.FormD);

        var stripped = new string(decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        Assert.Equal("cafe", stripped);
    }

    [Fact]
    public void DutchDiaeresisFolds()
    {
        var stripped = new string("pinguïn".Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        Assert.Equal("pinguin", stripped);
    }

    [Fact]
    public void IcuIsLoadedSoCultureAwareComparisonWorks()
    {
        // Under invariant globalization every culture collapses to InvariantCulture and
        // this lookup yields a culture whose name does not round-trip.
        var nl = CultureInfo.GetCultureInfo("nl-BE");

        Assert.Equal("nl-BE", nl.Name);
    }

    [Fact]
    public void BrusselsTimeZoneResolves()
    {
        // Invariant mode ships no tzdata, so this throws TimeZoneNotFoundException.
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

        Assert.NotNull(tz);
    }
}
