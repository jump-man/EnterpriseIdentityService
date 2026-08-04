using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

public sealed class UserRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 4, 7, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_ShouldPersistUserAndRoundTripValueObjects()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        User user = CreateUser();

        await using (ApplicationDbContext writeContext = database.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            repository.Add(user);
            await writeContext.SaveChangesAsync();
        }

        await using ApplicationDbContext readContext = database.CreateContext();
        User persistedUser = await readContext.Set<User>().SingleAsync();

        Assert.Equal(user.Id, persistedUser.Id);
        Assert.Equal(user.Email, persistedUser.Email);
        Assert.Equal(user.Username, persistedUser.Username);
        Assert.Equal(user.PasswordHash, persistedUser.PasswordHash);
        Assert.Equal(UserStatus.Pending, persistedUser.Status);
        Assert.Equal(CreatedAtUtc, persistedUser.CreatedAtUtc);
        Assert.Empty(persistedUser.DomainEvents);
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("other@example.com", false)]
    public async Task ExistsByEmailAsync_ShouldReturnExpectedResult(
        string email,
        bool expected)
    {
        await using SqliteTestDatabase database = await CreateDatabaseWithUserAsync();
        await using ApplicationDbContext context = database.CreateContext();
        var repository = new UserRepository(context);

        bool exists = await repository.ExistsByEmailAsync(
            Email.Create(email),
            CancellationToken.None);

        Assert.Equal(expected, exists);
    }

    [Theory]
    [InlineData("ali.dev", true)]
    [InlineData("other.user", false)]
    public async Task ExistsByUsernameAsync_ShouldReturnExpectedResult(
        string username,
        bool expected)
    {
        await using SqliteTestDatabase database = await CreateDatabaseWithUserAsync();
        await using ApplicationDbContext context = database.CreateContext();
        var repository = new UserRepository(context);

        bool exists = await repository.ExistsByUsernameAsync(
            Username.Create(username),
            CancellationToken.None);

        Assert.Equal(expected, exists);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldRejectDuplicateEmail()
    {
        await using SqliteTestDatabase database = await CreateDatabaseWithUserAsync();
        await using ApplicationDbContext context = database.CreateContext();
        var repository = new UserRepository(context);
        repository.Add(CreateUser(
            email: "user@example.com",
            username: "different.user"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldRejectDuplicateUsername()
    {
        await using SqliteTestDatabase database = await CreateDatabaseWithUserAsync();
        await using ApplicationDbContext context = database.CreateContext();
        var repository = new UserRepository(context);
        repository.Add(CreateUser(
            email: "different@example.com",
            username: "ali.dev"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Model_ShouldIgnoreDomainEvents()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        await using ApplicationDbContext context = database.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(User));

        Assert.NotNull(entityType);
        Assert.Null(entityType.FindProperty(nameof(User.DomainEvents)));
    }

    private static async Task<SqliteTestDatabase> CreateDatabaseWithUserAsync()
    {
        SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();

        await using ApplicationDbContext context = database.CreateContext();
        context.Set<User>().Add(CreateUser());
        await context.SaveChangesAsync();

        return database;
    }

    private static User CreateUser(
        string email = "user@example.com",
        string username = "ali.dev")
    {
        return User.Register(
            UserId.New(),
            Email.Create(email),
            Username.Create(username),
            PasswordHash.Create("HASHED-PASSWORD"),
            CreatedAtUtc);
    }
}
