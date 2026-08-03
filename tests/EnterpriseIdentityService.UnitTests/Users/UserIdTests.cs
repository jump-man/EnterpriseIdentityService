using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Users;

public sealed class UserIdTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new UserId(Guid.Empty));
    }

    [Fact]
    public void Constructor_ShouldPreserveValue_WhenValueIsNotEmpty()
    {
        var value = new Guid("a9702aa7-a5f1-4e11-aab7-12a64311d3d3");

        var userId = new UserId(value);

        Assert.Equal(value, userId.Value);
    }

    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        UserId userId = UserId.New();

        Assert.NotEqual(Guid.Empty, userId.Value);
    }
}
