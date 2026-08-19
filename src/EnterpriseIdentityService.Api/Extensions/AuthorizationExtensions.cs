using EnterpriseIdentityService.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }

    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        if (!EnterpriseIdentityService.Application.Authorization.Permissions.Contains(permission))
        {
            throw new ArgumentException("The permission is not in the application catalog.", nameof(permission));
        }

        return builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);
    }
}
