using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Application.Abstractions.Mailing;

public interface IPasswordResetEmailFactory
{
    EmailMessage Create(Email recipient, string rawToken, DateTimeOffset expiresAtUtc, string idempotencyKey);
}
