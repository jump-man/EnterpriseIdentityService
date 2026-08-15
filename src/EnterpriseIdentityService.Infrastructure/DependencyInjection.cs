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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
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
        services.AddOptions<AuthenticationSessionOptions>()
            .Bind(configuration.GetSection(AuthenticationSessionOptions.SectionName))
            .Validate(x => x.Lifetime > TimeSpan.Zero, "Session lifetime must be positive.")
            .ValidateOnStart();
        services.AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName))
            .Validate(x => x.TokenLifetime > TimeSpan.Zero && x.RequestCooldown >= TimeSpan.Zero &&
                Uri.TryCreate(x.PublicBaseUrl, UriKind.Absolute, out _), "Password recovery options are invalid.")
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
