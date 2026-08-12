using DoIt.Api.Configuration;
using DoIt.Api.Infrastructure.Persistence;
using DoIt.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

ApplyWebPushEnvironmentOverrides(builder.Configuration);
builder.Services.AddDoItConfiguration(builder.Configuration);
builder.Services.AddDoItPersistence(builder.Configuration);
builder.Services.AddDoItAuth(builder.Configuration);
builder.Services.AddDoItCors(builder.Configuration);
builder.Services.AddDoItRateLimiting();
builder.Services.AddDoItNotifications();
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

static void ApplyWebPushEnvironmentOverrides(IConfiguration configuration)
{
    ApplyEnvironmentOverride(configuration, "WebPush:Enabled", "DOIT_WEBPUSH_ENABLED");
    ApplyEnvironmentOverride(configuration, "WebPush:PublicKey", "DOIT_WEBPUSH_PUBLIC_KEY");
    ApplyEnvironmentOverride(configuration, "WebPush:PrivateKey", "DOIT_WEBPUSH_PRIVATE_KEY");
    ApplyEnvironmentOverride(configuration, "WebPush:Subject", "DOIT_WEBPUSH_SUBJECT");
    ApplyEnvironmentOverride(configuration, "WebPush:WorkerIntervalSeconds", "DOIT_WEBPUSH_WORKER_INTERVAL_SECONDS");
    ApplyEnvironmentOverride(configuration, "WebPush:LookbackSeconds", "DOIT_WEBPUSH_LOOKBACK_SECONDS");
    ApplyEnvironmentOverride(configuration, "WebPush:BatchSize", "DOIT_WEBPUSH_BATCH_SIZE");
    ApplyEnvironmentOverride(configuration, "WebPush:MaxAttempts", "DOIT_WEBPUSH_MAX_ATTEMPTS");
}

static void ApplyEnvironmentOverride(IConfiguration configuration, string key, string environmentVariable)
{
    var value = Environment.GetEnvironmentVariable(environmentVariable);
    if (!string.IsNullOrWhiteSpace(value))
    {
        configuration[key] = value;
    }
}

public partial class Program;
