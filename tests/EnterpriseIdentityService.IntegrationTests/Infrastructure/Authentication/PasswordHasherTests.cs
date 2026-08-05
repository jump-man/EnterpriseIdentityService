using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Authentication;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Authentication;

public sealed class PasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public void Hash_ShouldReturnNonEmptyHashThatDiffersFromPlaintext()
    {
        var passwordHasher = new PasswordHasher();

        PasswordHash result = passwordHasher.Hash(Password);

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.NotEqual(Password, result.Value);
    }

    [Fact]
    public void Hash_ShouldProduceDifferentSaltedHashesForSamePassword()
    {
        var passwordHasher = new PasswordHasher();

        PasswordHash first = passwordHasher.Hash(Password);
        PasswordHash second = passwordHasher.Hash(Password);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_ShouldAcceptCorrectPasswordAndRejectIncorrectPassword()
    {
        var passwordHasher = new PasswordHasher();
        PasswordHash hash = passwordHasher.Hash(Password);

        Assert.True(passwordHasher.Verify(Password, hash));
        Assert.False(passwordHasher.Verify("incorrect-password", hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_ShouldRejectMissingPassword(string? password)
    {
        var passwordHasher = new PasswordHasher();

        Assert.ThrowsAny<ArgumentException>(() => passwordHasher.Hash(password!));
    }
}
