using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens", "identity"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(x => x.Value, x => new RefreshTokenId(x)).ValueGeneratedNever();
        b.Property(x => x.SessionId).HasConversion(x => x.Value, x => new UserSessionId(x)).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.SessionId); b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        b.Property(x => x.ConsumedAtUtc).HasColumnType("datetimeoffset").IsConcurrencyToken();
        b.HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(x => x.IsConsumed); b.Ignore(x => x.DomainEvents);
    }
}
