using EnterpriseIdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;

internal sealed class EmailVerificationTokenConfiguration
    : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens", "identity");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasConversion(id => id.Value, value => new EmailVerificationTokenId(value))
            .ValueGeneratedNever();
        builder.Property(token => token.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.UserId)
            .IsUnique()
            .HasFilter("[ConsumedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");
        builder.Property(token => token.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.ConsumedAtUtc)
            .HasColumnType("datetimeoffset").IsConcurrencyToken();
        builder.Property(token => token.RevokedAtUtc)
            .HasColumnType("datetimeoffset").IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(token => token.IsConsumed);
        builder.Ignore(token => token.IsRevoked);
        builder.Ignore(token => token.DomainEvents);
    }
}
