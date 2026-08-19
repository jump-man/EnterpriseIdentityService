using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.UnitTests.Auditing;

public sealed class AuditEntryTests
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 8, 19, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldPreserveControlledSecurityContext()
    {
        UserId actor = UserId.New();
        UserId target = UserId.New();
        RoleId role = RoleId.New();
        UserSessionId session = UserSessionId.New();

        AuditEntry entry = AuditEntry.Create(
            AuditEntryId.New(),
            AuditEventType.RoleAssignedToUser,
            AuditOutcome.Success,
            null,
            OccurredAtUtc,
            "request-123",
            actor,
            target,
            role,
            session,
            "127.0.0.1",
            "Unit Test",
            "roles.read");

        Assert.Equal(actor, entry.ActorUserId);
        Assert.Equal(target, entry.TargetUserId);
        Assert.Equal(role, entry.RoleId);
        Assert.Equal(session, entry.SessionId);
        Assert.Equal(OccurredAtUtc, entry.OccurredAtUtc);
        Assert.Equal("roles.read", entry.Permission);
    }

    [Fact]
    public void Create_ShouldRejectNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            AuditEntryId.New(), AuditEventType.LoginSucceeded, AuditOutcome.Success, null,
            OccurredAtUtc.ToOffset(TimeSpan.FromHours(3)), "request-123"));
    }

    [Theory]
    [InlineData(AuditOutcome.Success, AuditReasonCode.InvalidCredentials)]
    [InlineData(AuditOutcome.Failure, null)]
    [InlineData(AuditOutcome.Detected, null)]
    public void Create_ShouldEnforceControlledOutcomeReasonPair(
        AuditOutcome outcome,
        AuditReasonCode? reason)
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            AuditEntryId.New(), AuditEventType.LoginFailed, outcome, reason,
            OccurredAtUtc, "request-123"));
    }

    [Fact]
    public void Create_ShouldRejectOversizedOrInvalidRequestContext()
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            AuditEntryId.New(), AuditEventType.LoginSucceeded, AuditOutcome.Success, null,
            OccurredAtUtc, new string('C', AuditEntry.MaximumCorrelationIdLength + 1)));
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            AuditEntryId.New(), AuditEventType.LoginSucceeded, AuditOutcome.Success, null,
            OccurredAtUtc, "request-123", ipAddress: "not-an-ip"));
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            AuditEntryId.New(), AuditEventType.LoginSucceeded, AuditOutcome.Success, null,
            OccurredAtUtc, "request-123",
            userAgent: new string('U', AuditEntry.MaximumUserAgentLength + 1)));
    }

    [Fact]
    public void PublicSurface_ShouldNotAcceptSensitiveOrArbitraryPayloads()
    {
        string[] parameterNames = typeof(AuditEntry).GetMethod(nameof(AuditEntry.Create))!
            .GetParameters()
            .Select(parameter => parameter.Name!)
            .ToArray();

        Assert.DoesNotContain(parameterNames, name =>
            name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("header", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("body", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("metadata", StringComparison.OrdinalIgnoreCase));
    }
}
