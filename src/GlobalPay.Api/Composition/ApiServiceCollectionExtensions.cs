using System.Security.Claims;
using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using GlobalPay.Api.Errors;
using GlobalPay.Api.Middleware;
using GlobalPay.Api.OpenApi;
using GlobalPay.Domain.Modules.Tenancy;
using GlobalPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GlobalPay.Api.Composition;

internal static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpContextAccessor();

        services.AddScoped<CorrelationIdMiddleware>();
        services.AddScoped<TenantResolutionMiddleware>();

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalPayExceptionHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, configuration, environment));

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(Policies.Approver, policy => policy.RequireRole(Roles.Approver))
            .AddPolicy(Policies.Finance, policy => policy.RequireRole(Roles.Finance))
            .AddPolicy(Policies.Auditor, policy => policy.RequireRole(Roles.Auditor, Roles.PlatformOperator, Roles.Admin))
            .AddPolicy(Policies.AdminOrApprover, policy => policy.RequireRole(Roles.Admin, Roles.Approver))
            .AddPolicy(Policies.FinanceOrApprover, policy => policy.RequireRole(Roles.Finance, Roles.Approver))
            .AddPolicy(
                Policies.FinanceOrAuditor,
                policy => policy.RequireRole(Roles.Finance, Roles.Auditor, Roles.PlatformOperator, Roles.Admin));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        services.AddHealthChecks()
            .AddDbContextCheck<GlobalPayDbContext>("sql", tags: ["ready"])
            .AddRedis(
                configuration.GetConnectionString("Redis") ?? "localhost:6379",
                name: "redis",
                tags: ["ready"]);

        services.AddOpenApi(options =>
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddObservability(configuration, environment);

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authority = configuration["Authentication:Authority"];

        options.Authority = authority;
        options.Audience = configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !environment.IsDevelopment();

        // Keep the raw JWT claim names. Without this, JwtBearer's inbound claim mapping rewrites `role`
        // to the long ClaimTypes.Role URI, so RoleClaimType = "role" never matches and every role policy
        // fails. The identity provider is configured to emit `role`, `tenant_id` and `preferred_username`.
        options.MapInboundClaims = false;

        // Keycloak derives the token issuer from the host used to request it, so a token fetched from the
        // host (localhost:8080) carries a different issuer than the in-network authority (keycloak:8080)
        // that serves metadata and signing keys. Accept both explicitly rather than disabling issuer
        // validation: signature, audience and lifetime checks all still apply.
        var validIssuers = new List<string>();

        if (!string.IsNullOrWhiteSpace(authority))
        {
            validIssuers.Add(authority);
        }

        var additionalIssuers = configuration.GetSection("Authentication:AdditionalValidIssuers").Get<string[]>();

        if (additionalIssuers is not null)
        {
            validIssuers.AddRange(additionalIssuers.Where(issuer => !string.IsNullOrWhiteSpace(issuer)));
        }

        options.TokenValidationParameters = new()
        {
            ValidateIssuer = validIssuers.Count > 0,
            ValidIssuers = validIssuers.Count > 0 ? validIssuers : null,
            ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "preferred_username",
            RoleClaimType = "role",
        };
    }

    private static void AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("GlobalPay.Api"));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();

            if (environment.IsDevelopment())
            {
                tracing.AddConsoleExporter();
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (environment.IsDevelopment())
            {
                metrics.AddConsoleExporter();
            }
        });

        var applicationInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            openTelemetry.UseAzureMonitor(options =>
                options.ConnectionString = applicationInsightsConnectionString);
        }
    }
}
