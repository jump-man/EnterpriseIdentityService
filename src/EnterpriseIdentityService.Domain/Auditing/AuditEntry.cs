using System.Net;
using EnterpriseIdentityService.Domain.Abstractions;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Domain.Auditing;

public sealed class AuditEntry : Entity<AuditEntryId>
{
    public const int MaximumCorrelationIdLength = 100;
    public const int MaximumIpAddressLength = 45;
    public const int MaximumUserAgentLength = 512;
    public const int MaximumPermissionLength = 100;

    private AuditEntry(
        AuditEntryId id,
        AuditEventType eventType,
        AuditOutcome outcome,
        AuditReasonCode? reasonCode,
        DateTimeOffset occurredAtUtc,
        UserId? actorUserId,
        UserId? targetUserId,
        RoleId? roleId,
        UserSessionId? sessionId,
        string correlationId,
        string? ipAddress,
        string? userAgent,
        string? permission)
        : base(id)
    {
        EventType = eventType;
        Outcome = outcome;
        ReasonCode = reasonCode;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        RoleId = roleId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Permission = permission;
        OccurredAtUtcTicks = occurredAtUtc.UtcTicks;
        SortId = id.Value.ToString("N");
    }

    public AuditEventType EventType { get; }

    public AuditOutcome Outcome { get; }

    public AuditReasonCode? ReasonCode { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public long OccurredAtUtcTicks { get; }

    public string SortId { get; }

    public UserId? ActorUserId { get; }

    public UserId? TargetUserId { get; }

    public RoleId? RoleId { get; }

    public UserSessionId? SessionId { get; }

    public string CorrelationId { get; }

    public string? IpAddress { get; }

    public string? UserAgent { get; }

    public string? Permission { get; }

    public static AuditEntry Create(
        AuditEntryId id,
        AuditEventType eventType,
        AuditOutcome outcome,
        AuditReasonCode? reasonCode,
        DateTimeOffset occurredAtUtc,
        string correlationId,
        UserId? actorUserId = null,
        UserId? targetUserId = null,
        RoleId? roleId = null,
        UserSessionId? sessionId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? permission = null)
    {
        if (!Enum.IsDefined(eventType))
        {
            throw new ArgumentOutOfRangeException(nameof(eventType));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (reasonCode is not null && !Enum.IsDefined(reasonCode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(reasonCode));
        }

        if ((outcome == AuditOutcome.Success) != (reasonCode is null))
        {
            throw new ArgumentException(
                "Successful audit entries cannot have a reason, and non-success entries require one.",
                nameof(reasonCode));
        }

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Audit timestamps must be UTC.", nameof(occurredAtUtc));
        }

        string safeCorrelationId = RequiredBounded(
            correlationId, MaximumCorrelationIdLength, nameof(correlationId));
        string? safeIpAddress = OptionalBounded(
            ipAddress, MaximumIpAddressLength, nameof(ipAddress));
        if (safeIpAddress is not null && !IPAddress.TryParse(safeIpAddress, out _))
        {
            throw new ArgumentException("The IP address is invalid.", nameof(ipAddress));
        }

        return new AuditEntry(
            id,
            eventType,
            outcome,
            reasonCode,
            occurredAtUtc,
            actorUserId,
            targetUserId,
            roleId,
            sessionId,
            safeCorrelationId,
            safeIpAddress,
            OptionalBounded(userAgent, MaximumUserAgentLength, nameof(userAgent)),
            OptionalBounded(permission, MaximumPermissionLength, nameof(permission)));
    }

    private static string RequiredBounded(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? OptionalBounded(string? value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        return RequiredBounded(value, maximumLength, parameterName);
    }
}
