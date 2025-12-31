using B2B.Application.DTOs;

namespace B2B.Application.Interfaces.Services;

/// <summary>
/// Service for managing JWT tokens with Redis-backed storage.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a new JWT token for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="email">The user's email.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="roles">The user's roles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A token result containing access and refresh tokens.</returns>
    Task<TokenResult> GenerateTokenAsync(
        string userId,
        string email,
        string tenantId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT token by checking its existence in Redis.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    Task<bool> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a JWT token by removing it from Redis.
    /// </summary>
    /// <param name="token">The JWT token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new token result with rotated tokens.</returns>
    Task<TokenResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Blacklists a token to prevent its use.
    /// </summary>
    /// <param name="jti">The JWT ID (jti claim) to blacklist.</param>
    /// <param name="expiry">The expiry time for the blacklist entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BlacklistTokenAsync(
        string jti,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}
