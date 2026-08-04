using EnterpriseIdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        Status = "Healthy",
        Service = "EnterpriseIdentityService"
    }));

app.Run();
