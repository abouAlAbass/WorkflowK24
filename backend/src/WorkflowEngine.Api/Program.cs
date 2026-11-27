using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Wolverine;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application;
using WorkflowEngine.Application.Features.Connections.Handlers;
using WorkflowEngine.Application.Features.Definitions.Handlers;
using WorkflowEngine.Application.Features.Diagnostics.Handlers;
using WorkflowEngine.Application.Features.Instances.Handlers;
using WorkflowEngine.Application.Features.SmartForms.Handlers;
using WorkflowEngine.Application.Features.Tasks.Handlers;
using WorkflowEngine.Infrastructure;
using WorkflowEngine.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Wolverine CQRS & In-Memory Messaging Configuration
builder.Host.UseWolverine(opts =>
{
    // Auto-discover *Handler classes + explicitly register plural *Handlers aggregates
    opts.Discovery.IncludeAssembly(typeof(WorkflowEngine.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeType<WorkflowDefinitionHandlers>();
    opts.Discovery.IncludeType<WorkflowInstanceHandlers>();
    opts.Discovery.IncludeType<WorkflowInstanceOperationalHandlers>();
    opts.Discovery.IncludeType<ViewFlowQueryHandlers>();
    opts.Discovery.IncludeType<WorkflowTaskHandlers>();
    opts.Discovery.IncludeType<WorkflowDiagnosticHandlers>();
    opts.Discovery.IncludeType<SmartFormHandler>();
    opts.Discovery.IncludeType<ConnectionHandlers>();
});

// 2. Clean Architecture Layer Registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<TaskEscalationBackgroundService>();

// 3. Authentication & JWT Bearer (Fail-Fast Secret Validation)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(OidcHttpClientNames.OidcProvider)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IOidcDiscoveryCache, OidcDiscoveryCache>();
builder.Services.AddSingleton<IOidcFlowStateStore, OidcFlowStateStore>();
builder.Services.AddScoped<IOidcAuthenticationService, OidcAuthenticationService>();

var secretKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "JWT secret key is not configured or is shorter than 32 bytes. Set 'Jwt:SecretKey' in configuration or environment variables.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "WorkflowEngine.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "WorkflowEngine.Frontend";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        // Re-validate the account on every request so deactivated/deleted users and role changes
        // take effect immediately instead of when the JWT expires.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var userId = principal is null ? string.Empty : CurrentActor.Id(principal);
                var users = context.HttpContext.RequestServices
                    .GetRequiredService<WorkflowEngine.Domain.Repositories.ISystemUserRepository>();
                var user = string.IsNullOrEmpty(userId) ? null : await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);

                if (user == null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("User account is inactive or no longer exists.");
                    return;
                }

                var tokenRole = principal!.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!string.Equals(tokenRole, user.Role, StringComparison.Ordinal))
                {
                    context.Fail("User role changed. Please sign in again.");
                }
            }
        };
    });

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    // One policy per permission (roles are mapped to permissions in Domain/Security/Permissions.cs).
    foreach (var permission in WorkflowEngine.Domain.Security.Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
    }

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Administrator", "BPM Administrator"));
});

// 4. Rate Limiting (Brute-Force & API Abuse Protection)
int authPermitLimit = int.TryParse(builder.Configuration["RateLimiting:AuthPermitLimit"], out var parsedAuthLimit) && parsedAuthLimit > 0
    ? parsedAuthLimit
    : 30;

builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    limiterOptions.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "local",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 5. OpenAPI / Swagger & Developer Tooling
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// 5. CORS for Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173" };

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

// Security Headers Middleware (CSP, Clickjacking, MIME-Sniffing & Referrer Protection)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!context.Request.Path.StartsWithSegments("/scalar"))
    {
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    }

    await next();
});

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health Checks
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    engine = "WorkflowEngine .NET 10"
})).WithTags("System");

// Map Feature Endpoint Groups
app.MapAuthEndpoints();
app.MapExternalAuthEndpoints();
app.MapDefinitionEndpoints();
app.MapInstanceEndpoints();
app.MapTaskEndpoints();
app.MapDelegationRuleEndpoints();
app.MapDiagnosticEndpoints();
app.MapSmartObjectEndpoints();
app.MapSmartFormEndpoints();
app.MapUserEndpoints();
app.MapAnalyticsEndpoints();
app.MapSettingEndpoints();
app.MapConnectionEndpoints();

// 6. Automatic EF Core Database Migration & Schema Initialization
await app.Services.InitializeDatabaseAsync();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
