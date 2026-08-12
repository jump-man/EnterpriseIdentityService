using System.Text;
using EnterpriseIdentityService.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using EnterpriseIdentityService.Application.Abstractions.Persistence;
using EnterpriseIdentityService.Domain.Users;

namespace EnterpriseIdentityService.Api.Extensions;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((jwt, configuredOptions) =>
            {
                JwtOptions options = configuredOptions.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        string? subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        string? version = context.Principal?.FindFirst("token_version")?.Value;
                        if (!Guid.TryParse(subject, out Guid userId) || !int.TryParse(version, out int tokenVersion))
                        {
                            context.Fail("Invalid authentication state.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        User? user = await repository.GetByIdAsync(new UserId(userId), context.HttpContext.RequestAborted);
                        if (user is null || user.TokenVersion != tokenVersion)
                            context.Fail("Invalid authentication state.");
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
