using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Application.Authentication.Login;
using EnterpriseIdentityService.Application.Users.GetCurrentUser;
using EnterpriseIdentityService.Application.Users.VerifyEmail;
using EnterpriseIdentityService.Application.Users.ResendVerificationEmail;
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddScoped<RegisterUserCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<GetCurrentUserQueryHandler>();
builder.Services.AddScoped<VerifyEmailCommandHandler>();
builder.Services.AddScoped<ResendVerificationEmailCommandHandler>();

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
app.UseRateLimiter();
app.UseAuthentication();
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

app.Run();

public partial class Program;
