using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class UsernameTests
{
    [Fact]
    public void Create_ShouldTrimSurroundingWhitespace()
    {
        Username username = Username.Create("  ali.dev  ");

        Assert.Equal("ali.dev", username.Value);
    }

    [Fact]
    public void Create_ShouldPreserveValidUsername()
    {
        Username username = Username.Create("AliDev");

        Assert.Equal("AliDev", username.Value);
    }

    [Fact]
    public void Create_ShouldAcceptSupportedCharacters()
    {
        Username username = Username.Create("Ali.01_dev-test");

        Assert.Equal("Ali.01_dev-test", username.Value);
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenValueIsShorterThanThreeCharacters()
    {
        Assert.Throws<ArgumentException>(() => Username.Create("ab"));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenValueIsLongerThanFiftyCharacters()
    {
        string value = new('a', 51);

        Assert.Throws<ArgumentException>(() => Username.Create(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowArgumentException_WhenValueIsMissing(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => Username.Create(value!));
    }

    [Theory]
    [InlineData("ali dev")]
    [InlineData("ali@dev")]
    public void Create_ShouldThrowArgumentException_WhenValueContainsUnsupportedCharacters(string value)
    {
        Assert.Throws<ArgumentException>(() => Username.Create(value));
    }

    [Fact]
    public void Create_ShouldProduceEqualUsernames_WhenNormalizedValuesAreEqual()
    {
        Username first = Username.Create("  ali.dev  ");
        Username second = Username.Create("ali.dev");

        Assert.Equal(first, second);
    }
}
