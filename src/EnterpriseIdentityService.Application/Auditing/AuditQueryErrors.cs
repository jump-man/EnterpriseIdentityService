using EnterpriseIdentityService.Application.Abstractions;

namespace EnterpriseIdentityService.Application.Auditing;

public static class AuditQueryErrors
{
    public static readonly Error InvalidPageSize = new(
        "Audit.InvalidPageSize", "Page size must be between 1 and 100.");
    public static readonly Error InvalidEventType = new(
        "Audit.InvalidEventType", "The audit event type is unknown.");
    public static readonly Error InvalidTimeRange = new(
        "Audit.InvalidTimeRange", "The audit time range must be positive and cannot exceed 90 days.");
    public static readonly Error InvalidCursor = new(
        "Audit.InvalidCursor", "The audit cursor is invalid.");
    public static readonly Error InvalidCorrelationId = new(
        "Audit.InvalidCorrelationId", "The correlation identifier is invalid.");
}
