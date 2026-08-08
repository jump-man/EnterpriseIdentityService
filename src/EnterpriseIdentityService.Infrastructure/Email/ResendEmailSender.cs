using EnterpriseIdentityService.Application.Abstractions.Mailing;
using Microsoft.Extensions.Options;
using Resend;
using ApplicationEmailMessage = EnterpriseIdentityService.Application.Abstractions.Mailing.EmailMessage;
using ResendEmailMessage = Resend.EmailMessage;

namespace EnterpriseIdentityService.Infrastructure.Mailing;

internal sealed class ResendEmailSender(
    IResend resend,
    IOptions<ResendOptions> options) : IEmailSender
{
    public async Task SendAsync(
        ApplicationEmailMessage message,
        CancellationToken cancellationToken)
    {
        var providerMessage = new ResendEmailMessage
        {
            From = $"{options.Value.FromName} <{options.Value.FromAddress}>",
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };
        providerMessage.To.Add(message.To);

        ResendResponse<Guid> response = await resend.EmailSendAsync(
            $"email-verification/{message.IdempotencyKey}",
            providerMessage,
            cancellationToken);

        if (!response.Success)
        {
            throw new EmailDeliveryException(
                "The email provider rejected the delivery request.",
                response.Exception);
        }
    }
}
