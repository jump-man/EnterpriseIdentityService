using EnterpriseIdentityService.Api.Endpoints.Authentication;
using EnterpriseIdentityService.Application.Users.Register;
using EnterpriseIdentityService.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RegisterUserCommandHandler>();

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

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        Status = "Healthy",
        Service = "EnterpriseIdentityService"
    }));

app.MapRegisterUserEndpoint();

app.Run();

public partial class Program;
