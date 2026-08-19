using EnterpriseIdentityService.Domain.Auditing;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", "identity");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => new AuditEntryId(value))
            .ValueGeneratedNever();
        builder.Property(entry => entry.EventType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(entry => entry.Outcome)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(entry => entry.ReasonCode)
            .HasConversion<string>()
            .HasMaxLength(64);
        builder.Property(entry => entry.OccurredAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(entry => entry.OccurredAtUtcTicks).IsRequired();
        builder.Property(entry => entry.SortId)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.ActorUserId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);
        builder.Property(entry => entry.TargetUserId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : null);
        builder.Property(entry => entry.RoleId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new RoleId(value.Value) : null);
        builder.Property(entry => entry.SessionId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserSessionId(value.Value) : null);
        builder.Property(entry => entry.CorrelationId)
            .HasMaxLength(AuditEntry.MaximumCorrelationIdLength)
            .IsRequired();
        builder.Property(entry => entry.IpAddress)
            .HasMaxLength(AuditEntry.MaximumIpAddressLength);
        builder.Property(entry => entry.UserAgent)
            .HasMaxLength(AuditEntry.MaximumUserAgentLength);
        builder.Property(entry => entry.Permission)
            .HasMaxLength(AuditEntry.MaximumPermissionLength);
        builder.Ignore(entry => entry.DomainEvents);

        builder.HasIndex(entry => new { entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending();
        builder.HasIndex(entry => new { entry.ActorUserId, entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending(false, true, true);
        builder.HasIndex(entry => new { entry.TargetUserId, entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending(false, true, true);
        builder.HasIndex(entry => new { entry.RoleId, entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending(false, true, true);
        builder.HasIndex(entry => new { entry.SessionId, entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending(false, true, true);
        builder.HasIndex(entry => new { entry.EventType, entry.OccurredAtUtcTicks, entry.SortId })
            .IsDescending(false, true, true);
        builder.HasIndex(entry => entry.CorrelationId);
    }
}
