using EnterpriseIdentityService.Application.Abstractions.Authentication;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Application.Abstractions.Mailing;
using EnterpriseIdentityService.Application.EmailVerification;
using EnterpriseIdentityService.Application.PasswordRecovery;
using EnterpriseIdentityService.Application.Authentication;
using EnterpriseIdentityService.Infrastructure.Authentication;
using EnterpriseIdentityService.Infrastructure.Mailing;
using EnterpriseIdentityService.Infrastructure.Persistence;
using EnterpriseIdentityService.Infrastructure.Persistence.Repositories;
using EnterpriseIdentityService.Application.Abstractions.Authorization;
using EnterpriseIdentityService.Infrastructure.Authorization;
using EnterpriseIdentityService.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Resend;

namespace EnterpriseIdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Database' was not found.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IAuthorizationSnapshotProvider, AuthorizationSnapshotProvider>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IAuditEntryQuery, AuditEntryQuery>();
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IEmailVerificationTokenGenerator, EmailVerificationTokenGenerator>();
        services.AddSingleton<IEmailVerificationTokenHasher, EmailVerificationTokenHasher>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<IPasswordResetTokenHasher, PasswordResetTokenHasher>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddSingleton<IVerificationEmailFactory, VerificationEmailFactory>();
        services.AddSingleton<IPasswordResetEmailFactory, PasswordResetEmailFactory>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenProvider, JwtAccessTokenProvider>();
        services.AddSingleton<IValidateOptions<AuthenticationSessionOptions>,
            AuthenticationSessionOptionsValidator>();
        services.AddOptions<AuthenticationSessionOptions>()
            .Bind(configuration.GetSection(AuthenticationSessionOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PasswordRecoveryOptions>,
            PasswordRecoveryOptionsValidator>();
        services.AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<EmailVerificationOptions>, EmailVerificationOptionsValidator>();
        services.AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection(EmailVerificationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ResendOptions>, ResendOptionsValidator>();
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName))
            .ValidateOnStart();
        services.AddResend(options =>
            options.ApiToken = configuration[$"{ResendOptions.SectionName}:ApiKey"] ?? string.Empty);
        services.AddTransient<IEmailSender, ResendEmailSender>();

        return services;
    }
}
