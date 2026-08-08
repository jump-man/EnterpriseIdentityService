namespace EnterpriseIdentityService.Application.Abstractions.Mailing;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    string IdempotencyKey);
