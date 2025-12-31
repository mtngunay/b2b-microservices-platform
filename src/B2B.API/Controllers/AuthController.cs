using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Aggregates;
using B2B.Domain.Exceptions;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.API.Controllers;

/// <summary>
/// Controller for authentication operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly WriteDbContext _dbContext;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITokenService tokenService,
        WriteDbContext dbContext,
        ILogger<AuthController> logger)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns JWT tokens.
    /// </summary>
    /// <param name="request">The login request containing email and password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Token result with access and refresh tokens.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Email and password are required.");
        }

        var email = request.Email.ToLowerInvariant();

        // Find user by email
        var user = await _dbContext.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt failed: User not found for email {Email}", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt failed: User {UserId} is inactive", user.Id);
            throw new UnauthorizedException("User account is inactive.");
        }

        // Verify password
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login attempt failed: Invalid password for user {UserId}", user.Id);
            throw new UnauthorizedException("Invalid email or password.");
        }

        // Get user roles
        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .ToList();

        // Generate tokens
        var tokenResult = await _tokenService.GenerateTokenAsync(
            user.Id.ToString(),
            user.Email,
            user.TenantId,
            roles,
            cancellationToken);

        // Record login
        user.RecordLogin();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Ok(tokenResult);
    }

    /// <summary>
    /// Logs out the current user by revoking their token.
    /// </summary>
    /// <param name="request">Optional logout request with access token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        // Get token from Authorization header or request body
        var token = request?.AccessToken;

        if (string.IsNullOrEmpty(token))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            await _tokenService.RevokeTokenAsync(token, cancellationToken);
            _logger.LogInformation("User logged out successfully");
        }

        return NoContent();
    }

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New token result with rotated tokens.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResult>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ValidationException("Refresh token is required.");
        }

        var tokenResult = await _tokenService.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        _logger.LogInformation("Token refreshed successfully");

        return Ok(tokenResult);
    }

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    private static bool VerifyPassword(string password, string passwordHash)
    {
        // Simple hash verification - in production, use a proper password hasher like BCrypt or Argon2
        var hash = ComputeHash(password);
        return hash == passwordHash;
    }

    /// <summary>
    /// Computes a SHA256 hash of the password.
    /// Note: In production, use BCrypt, Argon2, or ASP.NET Core Identity's password hasher.
    /// </summary>
    private static string ComputeHash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
