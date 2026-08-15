using EnterpriseIdentityService.Application.Abstractions.Messaging;
namespace EnterpriseIdentityService.Application.Authentication.Refresh;
public sealed record RefreshCommand(string RefreshToken) : ICommand<AuthenticationTokensResult>;
