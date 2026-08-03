using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class EmailTests
{
    [Fact]
    public void Create_ShouldTrimSurroundingWhitespace()
    {
        Email email = Email.Create("  user@example.com  ");

        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void Create_ShouldNormalizeLettersToLowercaseInvariant()
    {
        Email email = Email.Create("USER@EXAMPLE.COM");

        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void Create_ShouldProduceEqualEmails_WhenNormalizedValuesAreEquivalent()
    {
        Email first = Email.Create(" User@Example.com ");
        Email second = Email.Create("user@example.com");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowArgumentException_WhenValueIsMissing(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => Email.Create(value!));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenAtSignIsMissing()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("user.example.com"));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenMultipleAtSignsArePresent()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("user@example@com"));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenLocalPartIsMissing()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("@example.com"));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenDomainPartIsMissing()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("user@"));
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenAddressContainsInternalWhitespace()
    {
        Assert.Throws<ArgumentException>(() => Email.Create("user name@example.com"));
    }

    [Theory]
    [InlineData("user..name@example.com")]
    [InlineData("user@example..com")]
    public void Create_ShouldThrowArgumentException_WhenPartContainsConsecutiveDots(string value)
    {
        Assert.Throws<ArgumentException>(() => Email.Create(value));
    }
}
