using System.Reflection;

using ApplicationAssembly =
    EnterpriseIdentityService.Application.AssemblyReference;

using ContractsAssembly =
    EnterpriseIdentityService.Contracts.AssemblyReference;

using DomainAssembly =
    EnterpriseIdentityService.Domain.AssemblyReference;

using InfrastructureAssembly =
    EnterpriseIdentityService.Infrastructure.AssemblyReference;

namespace EnterpriseIdentityService.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_should_not_depend_on_other_solution_layers()
    {
        Assembly domainAssembly = typeof(DomainAssembly).Assembly;

        string[] forbiddenDependencies =
        [
            "EnterpriseIdentityService.Api",
            "EnterpriseIdentityService.Application",
            "EnterpriseIdentityService.Contracts",
            "EnterpriseIdentityService.Infrastructure",
            "Microsoft.AspNetCore",
            "Microsoft.AspNetCore.Authentication.JwtBearer",
            "Microsoft.IdentityModel.Tokens",
            "System.IdentityModel.Tokens.Jwt"
        ];

        AssertDoesNotReference(domainAssembly, forbiddenDependencies);
    }

    [Fact]
    public void Application_should_not_depend_on_outer_layers()
    {
        Assembly applicationAssembly = typeof(ApplicationAssembly).Assembly;

        string[] forbiddenDependencies =
        [
            "EnterpriseIdentityService.Api",
            "EnterpriseIdentityService.Contracts",
            "EnterpriseIdentityService.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Microsoft.AspNetCore.Authentication.JwtBearer",
            "Microsoft.IdentityModel.Tokens",
            "System.IdentityModel.Tokens.Jwt"
        ];

        AssertDoesNotReference(applicationAssembly, forbiddenDependencies);
    }

    [Fact]
    public void Contracts_should_not_depend_on_other_solution_layers()
    {
        Assembly contractsAssembly = typeof(ContractsAssembly).Assembly;

        string[] forbiddenDependencies =
        [
            "EnterpriseIdentityService.Api",
            "EnterpriseIdentityService.Application",
            "EnterpriseIdentityService.Domain",
            "EnterpriseIdentityService.Infrastructure"
        ];

        AssertDoesNotReference(contractsAssembly, forbiddenDependencies);
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_api()
    {
        Assembly infrastructureAssembly = typeof(InfrastructureAssembly).Assembly;

        AssertDoesNotReference(
            infrastructureAssembly,
            ["EnterpriseIdentityService.Api"]);
    }

    private static void AssertDoesNotReference(
        Assembly assembly,
        IEnumerable<string> forbiddenDependencies)
    {
        HashSet<string> referencedAssemblies = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        string[] violations = forbiddenDependencies
            .Where(referencedAssemblies.Contains)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} must not reference: " +
            $"{string.Join(", ", violations)}");
    }
}
