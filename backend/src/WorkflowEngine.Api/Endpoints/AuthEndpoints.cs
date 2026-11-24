using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WorkflowEngine.Api.Auth;

namespace WorkflowEngine.Api.Endpoints;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string UserId, string Name, string Email, string Role, string Group);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", (LoginRequest request, IJwtTokenService tokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email is required." });
            }

            // Demo/connected authentication directory
            string email = request.Email.Trim().ToLowerInvariant();
            string userId = $"usr-{Math.Abs(email.GetHashCode()):X8}";
            string name = email.Contains("adel") ? "Adel Sebti" :
                          email.Contains("karim") ? "Karim Khmissi" :
                          email.Contains("mohamed") ? "Mohamed Fetni" : "Workflow Administrator";

            string role = email.Contains("admin") ? "BPM Administrator" : "Process Manager";
            string userGroup = email.Contains("finance") ? "Finance Managers" : "Engineering";

            var token = tokenService.GenerateToken(userId, email, role, userGroup);

            return Results.Ok(new LoginResponse(token, userId, name, email, role, userGroup));
        })
        .WithName("Login")
        .WithSummary("Authenticates a user and returns a JWT Bearer token.");

        return app;
    }
}
