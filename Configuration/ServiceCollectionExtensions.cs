using System.Text;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Application.Services;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DoIt.Api.Security;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;

namespace DoIt.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDoItConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));
        services.Configure<DoItSettings>(configuration.GetSection(DoItSettings.SectionName));
        services.Configure<HouseholdIntegrationSettings>(configuration.GetSection(HouseholdIntegrationSettings.SectionName));
        return services;
    }

    public static IServiceCollection AddDoItPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? new DatabaseSettings();
        var databasePath = Path.GetFullPath(databaseSettings.DatabasePath, AppContext.BaseDirectory);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContext<DoItDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        return services;
    }

    public static IServiceCollection AddDoItAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey must be configured and contain at least 32 characters.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));
        var householdSettings = configuration.GetSection(HouseholdIntegrationSettings.SectionName).Get<HouseholdIntegrationSettings>() ?? new HouseholdIntegrationSettings();
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromSeconds(30)
                 };
             })
            .AddJwtBearer(HouseholdIntegrationPolicies.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.EventsType = typeof(HouseholdIntegrationAuthenticationEvents);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidIssuer = HouseholdIntegrationTokenCodec.Issuer(jwtSettings),
                    ValidAudience = householdSettings.ClientId,
                    IssuerSigningKey = HouseholdIntegrationTokenCodec.SigningKey(jwtSettings),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(5)
                };
            });
        services.AddAuthorization(options =>
        {
            AddHouseholdPolicy(options, HouseholdIntegrationPolicies.ProfileRead, HouseholdIntegrationScopes.ProfileRead);
            AddHouseholdPolicy(options, HouseholdIntegrationPolicies.TasksRead, HouseholdIntegrationScopes.TasksRead);
            AddHouseholdPolicy(options, HouseholdIntegrationPolicies.TasksComplete, HouseholdIntegrationScopes.TasksComplete);
            AddHouseholdPolicy(options, HouseholdIntegrationPolicies.TasksUndo, HouseholdIntegrationScopes.TasksUndo);
            AddHouseholdPolicy(options, HouseholdIntegrationPolicies.TasksCreate, HouseholdIntegrationScopes.TasksCreate);
        });
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<INowService, NowService>();
        services.AddScoped<IOccurrenceService, OccurrenceService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<ITaskActionService, TaskActionService>();
        services.AddScoped<IThemePreferenceService, ThemePreferenceService>();
        services.AddScoped<IXpService, XpService>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<IHouseholdIntegrationService, HouseholdIntegrationService>();
        services.AddScoped<IHouseholdConnectionService, HouseholdConnectionService>();
        services.AddScoped<HouseholdIntegrationAuthenticationEvents>();
        services.AddScoped<HouseholdIntegrationTokenCodec>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        return services;
    }

    public static IServiceCollection AddDoItRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("household-authorize", context => CreateFixedWindow(context, 10));
            options.AddPolicy("household-token", context => CreateFixedWindow(context, 30));
            options.AddPolicy("household-revoke", context => CreateFixedWindow(context, 30));
        });
        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindow(HttpContext context, int permitLimit)
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static void AddHouseholdPolicy(AuthorizationOptions options, string policyName, string scope)
    {
        options.AddPolicy(policyName, policy => policy
            .AddAuthenticationSchemes(HouseholdIntegrationPolicies.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim("scope", scope));
    }

    public static IServiceCollection AddDoItCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (corsSettings.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
                }
                else
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }
}
