using Energieonderbrekingen.Web.Api;

namespace Energieonderbrekingen.Tests;

public class PostalCodeTests
{
    [Theory]
    [InlineData("1234AB", true)]
    [InlineData("1234 ab", true)]
    [InlineData(" 1234ab ", true)]
    [InlineData("1234", true)]
    [InlineData("0123AB", false)]
    [InlineData("123AB", false)]
    [InlineData("1234A", false)]
    [InlineData("1234ABC", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates(string? input, bool expected) => Assert.Equal(expected, PostalCode.IsValid(input));

    [Fact]
    public void Normalizes() => Assert.Equal("1234AB", PostalCode.Normalize(" 1234 ab"));

    [Theory]
    [InlineData("4321AB", "4321AB", true)]   // PC6 == PC6
    [InlineData("4321AB", "4321AC", false)]  // andere straat, zelfde PC4
    [InlineData("4321AB", "4321", true)]     // onderbreking op PC4-niveau
    [InlineData("4321", "4321AC", true)]     // abonnee op PC4-niveau
    [InlineData("4321", "4322AC", false)]
    [InlineData("4321ab", "4321 AB", true)]  // hoofdletters/spaties
    public void Matches(string subscriber, string disruption, bool expected) =>
        Assert.Equal(expected, PostalCode.Matches(subscriber, disruption));

    [Fact]
    public void Affects_checks_semicolon_separated_list()
    {
        var d = new Disruption
        {
            Id = "x",
            Location = new() { Features = new() { Properties = new() { PostalCode = "4321AB;4321AC" } } },
        };

        Assert.True(PostalCode.Affects(d, "4321AC"));
        Assert.False(PostalCode.Affects(d, "4321AD"));
        Assert.True(PostalCode.Affects(d, "4321"));
    }
}
