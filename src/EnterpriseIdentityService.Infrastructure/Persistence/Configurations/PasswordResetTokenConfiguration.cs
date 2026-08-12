using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens", "identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(x => x.Value, x => new PasswordResetTokenId(x)).ValueGeneratedNever();
        builder.Property(x => x.UserId).HasConversion(x => x.Value, x => new UserId(x)).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[ConsumedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(x => x.ConsumedAtUtc).HasColumnType("datetimeoffset").IsConcurrencyToken();
        builder.Property(x => x.RevokedAtUtc).HasColumnType("datetimeoffset").IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(x => x.IsConsumed); builder.Ignore(x => x.IsRevoked); builder.Ignore(x => x.DomainEvents);
    }
}
