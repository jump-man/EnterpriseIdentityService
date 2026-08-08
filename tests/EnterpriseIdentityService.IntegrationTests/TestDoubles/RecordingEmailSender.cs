using System.Collections.Concurrent;
using EnterpriseIdentityService.Application.Abstractions.Mailing;

namespace EnterpriseIdentityService.IntegrationTests.TestDoubles;

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _)) { }
    }
}
