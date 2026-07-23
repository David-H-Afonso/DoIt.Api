using DoIt.Api.Configuration;
using DoIt.Api.Infrastructure.Persistence;
using DoIt.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDoItConfiguration(builder.Configuration);
builder.Services.AddDoItPersistence(builder.Configuration);
builder.Services.AddDoItAuth(builder.Configuration);
builder.Services.AddDoItCors(builder.Configuration);
builder.Services.AddDoItRateLimiting();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseStartupHelper.ApplyMigrationsAsync(app.Services);
}

await app.RunAsync();

public partial class Program;
