using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class PasswordHashTests
{
    [Fact]
    public void Create_ShouldPreserveValidHash()
    {
        const string value = "FAKE-HASH-123";

        PasswordHash passwordHash = PasswordHash.Create(value);

        Assert.Equal(value, passwordHash.Value);
    }

    [Fact]
    public void Create_ShouldNotTrimOrLowercaseValue()
    {
        const string value = "  FaKe-HaSh  ";

        PasswordHash passwordHash = PasswordHash.Create(value);

        Assert.Equal(value, passwordHash.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowArgumentException_WhenValueIsMissing(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => PasswordHash.Create(value!));
    }

    [Fact]
    public void Create_ShouldProduceEqualHashes_WhenValuesAreIdentical()
    {
        PasswordHash first = PasswordHash.Create("FAKE-HASH");
        PasswordHash second = PasswordHash.Create("FAKE-HASH");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_ShouldProduceDifferentHashes_WhenValuesDifferByCase()
    {
        PasswordHash first = PasswordHash.Create("FAKE-HASH");
        PasswordHash second = PasswordHash.Create("fake-hash");

        Assert.NotEqual(first, second);
    }
}
