using System.Net;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.PasswordRecovery;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class PasswordResetEmailFactory(IOptions<PasswordRecoveryOptions> options) : IPasswordResetEmailFactory
{
    public EmailMessage Create(Email recipient, string rawToken, DateTimeOffset expiresAtUtc, string idempotencyKey)
    {
        string url = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        string html = $"<p>Reset your password.</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Continue password reset</a></p><p>For the backend-only Development flow, submit this token to POST /api/users/reset-password: <code>{WebUtility.HtmlEncode(rawToken)}</code></p><p>This link expires at {WebUtility.HtmlEncode(expiresAtUtc.ToString("O"))}. If you did not request this, ignore this email.</p>";
        string text = $"Reset your password: {url}{Environment.NewLine}For the backend-only Development flow, submit this token to POST /api/users/reset-password: {rawToken}{Environment.NewLine}This link expires at {expiresAtUtc:O}. If you did not request this, ignore this email.";
        return new EmailMessage(recipient.Value, "Reset your Enterprise Identity Service password", html, text, idempotencyKey);
    }
}
