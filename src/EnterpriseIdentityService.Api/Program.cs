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
using EnterpriseIdentityService.Api.Extensions;
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
builder.Services.AddProblemDetails();
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
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

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status500InternalServerError
});

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

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        Status = "Healthy",
        Service = "EnterpriseIdentityService"
    }));

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

app.Run();

public partial class Program;
