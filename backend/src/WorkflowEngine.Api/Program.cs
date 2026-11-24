using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Wolverine;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application;
using WorkflowEngine.Infrastructure;
using WorkflowEngine.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Wolverine CQRS & In-Memory Messaging Configuration
builder.Host.UseWolverine(opts =>
{
    // Auto-discover handlers in the Application assembly
    opts.Discovery.IncludeAssembly(typeof(WorkflowEngine.Application.DependencyInjection).Assembly);
});

// 2. Clean Architecture Layer Registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 3. Authentication & JWT Bearer
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var secretKey = builder.Configuration["Jwt:SecretKey"] ?? "WorkflowEngine_SuperSecretSecurityKey_AtLeast32BytesLong!";
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
    });

builder.Services.AddAuthorization();

// 4. OpenAPI / Swagger & Developer Tooling
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

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("AllowFrontend");

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
app.MapDefinitionEndpoints();
app.MapInstanceEndpoints();
app.MapTaskEndpoints();
app.MapDiagnosticEndpoints();
app.MapSmartObjectEndpoints();
app.MapUserEndpoints();
app.MapAnalyticsEndpoints();
app.MapSettingEndpoints();

// 6. Automatic EF Core Database Migration & Schema Initialization
await app.Services.InitializeDatabaseAsync();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
