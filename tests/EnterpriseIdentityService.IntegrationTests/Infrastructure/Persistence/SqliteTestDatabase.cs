using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    private SqliteTestDatabase(
        SqliteConnection connection,
        DbContextOptions<ApplicationDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var database = new SqliteTestDatabase(connection, options);

        await using ApplicationDbContext context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return database;
    }

    public ApplicationDbContext CreateContext() => new(_options);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
