using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Application.Authentication.Login;
using EnterpriseIdentityService.Application.Users.GetCurrentUser;
using EnterpriseIdentityService.Application.Users.VerifyEmail;
using EnterpriseIdentityService.Application.Users.ResendVerificationEmail;
using EnterpriseIdentityService.Application.Users.ForgotPassword;
using EnterpriseIdentityService.Application.Users.ResetPassword;
using EnterpriseIdentityService.Application.Users.ChangePassword;
using EnterpriseIdentityService.Application.Authentication.Refresh;
using EnterpriseIdentityService.Application.Authentication.Logout;
using EnterpriseIdentityService.Application.Authentication.LogoutAll;
using EnterpriseIdentityService.Api.Endpoints.Users;
using EnterpriseIdentityService.Api.Endpoints.Authorization;
using EnterpriseIdentityService.Application.Authorization.PermissionCatalog;
using EnterpriseIdentityService.Application.Authorization.Roles;
using EnterpriseIdentityService.Application.Authorization.UserRoles;
using EnterpriseIdentityService.Api.Extensions;
using EnterpriseIdentityService.Api.Auditing;
using EnterpriseIdentityService.Application.Abstractions.Auditing;
using EnterpriseIdentityService.Application.Auditing;
using EnterpriseIdentityService.Api.Endpoints.Auditing;
using EnterpriseIdentityService.Api.Health;
using EnterpriseIdentityService.Api.Observability;
using EnterpriseIdentityService.Infrastructure;

using Microsoft.OpenApi;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter the JWT access token."
        });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.OperationFilter<AllowAnonymousOperationFilter>();
});
builder.Services.AddOperationalObservability(builder.Configuration, builder.Environment);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("verification-resend", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("password-recovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true
            }));
    options.AddPolicy("password-change", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true
            }));
    options.AddPolicy("token-refresh", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("session-security", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("authorization-mutation", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("audit-read", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();
builder.Services.AddScoped<AuditRecorder>();
builder.Services.AddJwtAuthentication();
builder.Services.AddPermissionAuthorization();
builder.Services.AddScoped<RegisterUserCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<GetCurrentUserQueryHandler>();
builder.Services.AddScoped<VerifyEmailCommandHandler>();
builder.Services.AddScoped<ResendVerificationEmailCommandHandler>();
builder.Services.AddScoped<ForgotPasswordCommandHandler>();
builder.Services.AddScoped<ResetPasswordCommandHandler>();
builder.Services.AddScoped<ChangePasswordCommandHandler>();
builder.Services.AddScoped<RefreshCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();
builder.Services.AddScoped<LogoutAllCommandHandler>();
builder.Services.AddScoped<ListPermissionsQueryHandler>();
builder.Services.AddScoped<ListRolesQueryHandler>();
builder.Services.AddScoped<GetRoleQueryHandler>();
builder.Services.AddScoped<CreateRoleCommandHandler>();
builder.Services.AddScoped<RenameRoleCommandHandler>();
builder.Services.AddScoped<SetRoleEnabledCommandHandler>();
builder.Services.AddScoped<DeleteRoleCommandHandler>();
builder.Services.AddScoped<ReplaceRolePermissionsCommandHandler>();
builder.Services.AddScoped<ListUserRolesQueryHandler>();
builder.Services.AddScoped<AssignRoleCommandHandler>();
builder.Services.AddScoped<RemoveRoleCommandHandler>();
builder.Services.AddScoped<QueryAuditEntriesQueryHandler>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapOperationalHealthEndpoints();

app.MapRegisterUserEndpoint();
app.MapLoginEndpoint();
app.MapGetCurrentUserEndpoint();
app.MapVerifyEmailEndpoint();
app.MapResendVerificationEmailEndpoint();
app.MapForgotPasswordEndpoint();
app.MapResetPasswordEndpoint();
app.MapChangePasswordEndpoint();
app.MapRefreshEndpoint();
app.MapLogoutEndpoint();
app.MapLogoutAllEndpoint();
app.MapAuthorizationEndpoints();
app.MapAuditEndpoint();

app.Run();

public partial class Program;
