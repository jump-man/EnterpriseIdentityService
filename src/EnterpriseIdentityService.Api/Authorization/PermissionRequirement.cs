using Microsoft.AspNetCore.Authorization;

namespace EnterpriseIdentityService.Api.Authorization;

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
