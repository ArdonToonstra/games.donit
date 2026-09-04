using DonitGames.Core.Rooms;

namespace DonitGames.Core.Tests.Rooms;

public class RoomCodeGeneratorTests
{
    [Fact]
    public void Generate_produces_a_four_character_code()
    {
        var code = RoomCodeGenerator.Generate(new Random(1));
        Assert.Equal(4, code.Length);
    }

    [Fact]
    public void Generate_only_uses_alphabet_characters()
    {
        var rng = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var code = RoomCodeGenerator.Generate(rng);
            Assert.All(code, c => Assert.Contains(c, RoomCodeGenerator.Alphabet));
        }
    }

    [Theory]
    [InlineData('0')]
    [InlineData('O')]
    [InlineData('1')]
    [InlineData('I')]
    [InlineData('L')]
    [InlineData('5')]
    [InlineData('S')]
    [InlineData('2')]
    [InlineData('Z')]
    [InlineData('8')]
    [InlineData('B')]
    public void Alphabet_excludes_characters_that_get_misheard_aloud(char excluded)
    {
        Assert.DoesNotContain(excluded, RoomCodeGenerator.Alphabet);
    }
}
