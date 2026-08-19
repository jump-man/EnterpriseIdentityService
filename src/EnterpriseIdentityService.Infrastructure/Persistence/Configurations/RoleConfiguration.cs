using EnterpriseIdentityService.Application.Authorization;
using EnterpriseIdentityService.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseIdentityService.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id)
            .HasConversion(id => id.Value, value => new RoleId(value))
            .ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(Role.MaximumNameLength).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(Role.MaximumNameLength).IsRequired();
        builder.HasIndex(role => role.NormalizedName).IsUnique();
        builder.Property(role => role.IsSystem).IsRequired();
        builder.Property(role => role.IsEnabled).IsRequired();
        builder.Property(role => role.Version).HasDefaultValue(0).IsConcurrencyToken();
        builder.Ignore(role => role.DomainEvents);

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(role => role.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(new
        {
            Id = BuiltInRoles.AdministratorId,
            Name = BuiltInRoles.AdministratorName,
            NormalizedName = BuiltInRoles.AdministratorName.ToUpperInvariant(),
            IsSystem = true,
            IsEnabled = true,
            Version = 0
        });

    }
}
