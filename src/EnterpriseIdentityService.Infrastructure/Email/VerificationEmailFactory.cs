using System.Net;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.EmailVerification;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.Extensions.Options;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class VerificationEmailFactory(IOptions<EmailVerificationOptions> options)
    : IVerificationEmailFactory
{
    public EmailMessage Create(
        Email recipient,
        string rawToken,
        DateTimeOffset expiresAtUtc,
        string idempotencyKey)
    {
        string url = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        string encodedUrl = WebUtility.HtmlEncode(url);
        string encodedToken = WebUtility.HtmlEncode(rawToken);
        string encodedExpiry = WebUtility.HtmlEncode(expiresAtUtc.ToString("O"));
        const string subject = "Verify your Enterprise Identity Service account";
        string html = $"<p>Verify your Enterprise Identity Service account.</p><p><a href=\"{encodedUrl}\">Continue verification</a></p><p>For the backend-only Development flow, submit this token to POST /api/users/verify-email: <code>{encodedToken}</code></p><p>This link expires at {encodedExpiry}. If you did not register, ignore this email.</p>";
        string text = $"Verify your Enterprise Identity Service account: {url}{Environment.NewLine}For the backend-only Development flow, submit this token to POST /api/users/verify-email: {rawToken}{Environment.NewLine}This link expires at {expiresAtUtc:O}. If you did not register, ignore this email.";
        return new EmailMessage(recipient.Value, subject, html, text, idempotencyKey);
    }
}
