using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Infrastructure;
using EnterpriseIdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseIdentityService.IntegrationTests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterRequiredServicesWithSharedScopedContext()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Server=localhost;Database=Test;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();

        ApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IUserRepository repository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IUnitOfWork unitOfWork =
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        Assert.NotNull(repository);
        Assert.NotNull(passwordHasher);
        Assert.Same(context, unitOfWork);
    }

    [Fact]
    public void AddInfrastructure_ShouldRejectMissingConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        Assert.Equal("Connection string 'Database' was not found.", exception.Message);
    }
}
