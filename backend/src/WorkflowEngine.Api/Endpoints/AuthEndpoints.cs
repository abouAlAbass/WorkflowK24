using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WorkflowEngine.Api.Auth;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Api.Endpoints;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string UserId, string Name, string Email, string Role, string Group);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (
            LoginRequest request,
            ISystemUserRepository userRepository,
            IJwtTokenService tokenService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Email and password are required." });
            }

            string email = request.Email.Trim().ToLowerInvariant();
            var user = await userRepository.GetByEmailAsync(email, ct);

            if (user == null || !user.VerifyPassword(request.Password))
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { error = "User account is inactive." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var token = tokenService.GenerateToken(user.Id, user.Email, user.Role, user.Group);

            return Results.Ok(new LoginResponse(token, user.Id, user.Name, user.Email, user.Role, user.Group));
        })
        .AllowAnonymous()
        .RequireRateLimiting("AuthPolicy")
        .WithName("Login")
        .WithSummary("Authenticates a user against PBKDF2 credentials and returns a JWT Bearer token.");

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            ClaimsPrincipal principal,
            ISystemUserRepository userRepository,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                request.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { error = "Current password and a new password of at least 8 characters are required." });
            }

            var email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.Unauthorized();
            }

            var user = await userRepository.GetByEmailAsync(email, ct);
            if (user == null || !user.VerifyPassword(request.CurrentPassword))
            {
                return Results.BadRequest(new { error = "Current password is incorrect." });
            }

            user.SetPassword(request.NewPassword);
            await userRepository.UpdateAsync(user, ct);

            return Results.Ok(new { message = "Password updated successfully." });
        })
        .RequireAuthorization()
        .WithName("ChangePassword")
        .WithSummary("Updates the authenticated user's PBKDF2 password hash.");

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            ISystemUserRepository userRepository,
            CancellationToken ct) =>
        {
            var email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.Unauthorized();
            }

            var user = await userRepository.GetByEmailAsync(email, ct);
            if (user == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                userId = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role,
                group = user.Group,
                status = user.Status,
                avatarUrl = user.AvatarUrl
            });
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Returns the currently authenticated user's profile.");

        return app;
    }
}
