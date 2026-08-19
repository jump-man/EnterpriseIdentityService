using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;
using EnterpriseIdentityService.Domain.Roles;
using EnterpriseIdentityService.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseIdentityService.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    internal DbSet<User> Users => Set<User>();
    internal DbSet<EmailVerificationToken> EmailVerificationTokens =>
        Set<EmailVerificationToken>();
    internal DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    internal DbSet<UserSession> UserSessions => Set<UserSession>();
    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    internal DbSet<Role> Roles => Set<Role>();
    internal DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    internal DbSet<UserRole> UserRoles => Set<UserRole>();
    internal DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuditEntriesAreAppendOnly();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException(
                "The data was changed by another operation.", exception);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditEntriesAreAppendOnly();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException(
                "The data was changed by another operation.", exception);
        }
    }

    private void EnsureAuditEntriesAreAppendOnly()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Security audit entries are append-only.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
