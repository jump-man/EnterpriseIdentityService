namespace EnterpriseIdentityService.Application.Abstractions.Mailing;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
