using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("UserSessions", "identity"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(x => x.Value, x => new UserSessionId(x)).ValueGeneratedNever();
        b.Property(x => x.UserId).HasConversion(x => x.Value, x => new UserId(x)).IsRequired();
        b.HasIndex(x => x.UserId); b.Property(x => x.TokenVersionAtIssue).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        b.Property(x => x.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
        b.Property(x => x.LastUsedAtUtc).HasColumnType("datetimeoffset").IsConcurrencyToken().IsRequired();
        b.Property(x => x.RevokedAtUtc).HasColumnType("datetimeoffset").IsConcurrencyToken();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(x => x.IsRevoked); b.Ignore(x => x.DomainEvents);
    }
}
