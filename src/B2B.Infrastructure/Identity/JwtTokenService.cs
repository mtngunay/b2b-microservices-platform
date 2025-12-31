using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace B2B.Infrastructure.Identity;

/// <summary>
/// JWT token service with Redis-backed token storage for stateless authentication.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly ICacheService _cacheService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    private const string TokenKeyPrefix = "token";
    private const string RefreshTokenKeyPrefix = "refresh_token";
    private const string BlacklistKeyPrefix = "blacklist";

    /// <summary>
    /// Initializes a new instance of JwtTokenService.
    /// </summary>
    /// <param name="cacheService">The cache service for token storage.</param>
    /// <param name="jwtOptions">JWT configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public JwtTokenService(
        ICacheService cacheService,
        IOptions<JwtOptions> jwtOptions,
        ILogger<JwtTokenService> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <inheritdoc />
    public async Task<TokenResult> GenerateTokenAsync(
        string userId,
        string email,
        string tenantId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var jti = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var accessTokenExpiry = now.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);
        var refreshTokenExpiry = now.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        // Build claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("tenant_id", tenantId)
        };

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Generate access token
        var accessToken = GenerateJwtToken(claims, accessTokenExpiry);

        // Generate refresh token
        var refreshToken = GenerateRefreshToken();

        // Store token data in Redis
        var tokenData = new TokenData
        {
            UserId = userId,
            TenantId = tenantId,
            Email = email,
            Roles = roles.ToList(),
            Jti = jti,
            ExpiresAt = accessTokenExpiry
        };

        await _cacheService.SetAsync(
            $"{TokenKeyPrefix}:{jti}",
            tokenData,
            TimeSpan.FromMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            cancellationToken);

        // Store refresh token mapping
        var refreshTokenData = new RefreshTokenData
        {
            UserId = userId,
            TenantId = tenantId,
            Email = email,
            Roles = roles.ToList(),
            Jti = jti,
            ExpiresAt = refreshTokenExpiry
        };

        await _cacheService.SetAsync(
            $"{RefreshTokenKeyPrefix}:{refreshToken}",
            refreshTokenData,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenExpirationDays),
            cancellationToken);

        _logger.LogInformation(
            "Generated tokens for user {UserId} in tenant {TenantId}",
            userId,
            tenantId);

        return TokenResult.Create(
            accessToken,
            refreshToken,
            _jwtOptions.AccessTokenExpirationMinutes * 60);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, validate the JWT signature and claims
            var principal = ValidateJwtToken(token);
            if (principal == null)
            {
                _logger.LogWarning("JWT token validation failed - invalid signature or claims");
                return false;
            }

            // Extract JTI from token
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("JWT token validation failed - missing JTI claim");
                return false;
            }

            // Check if token is blacklisted
            var isBlacklisted = await _cacheService.ExistsAsync(
                $"{BlacklistKeyPrefix}:{jti}",
                cancellationToken);

            if (isBlacklisted)
            {
                _logger.LogWarning("JWT token validation failed - token is blacklisted. JTI: {Jti}", jti);
                return false;
            }

            // Check if token exists in Redis (stateless validation)
            var tokenData = await _cacheService.GetAsync<TokenData>(
                $"{TokenKeyPrefix}:{jti}",
                cancellationToken);

            if (tokenData == null)
            {
                _logger.LogWarning("JWT token validation failed - token not found in Redis. JTI: {Jti}", jti);
                return false;
            }

            // Check if token has expired
            if (tokenData.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("JWT token validation failed - token expired. JTI: {Jti}", jti);
                return false;
            }

            _logger.LogDebug("JWT token validated successfully. JTI: {Jti}", jti);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating JWT token");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = ValidateJwtToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Cannot revoke token - invalid token");
                return;
            }

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("Cannot revoke token - missing JTI claim");
                return;
            }

            // Remove token from Redis
            await _cacheService.RemoveAsync($"{TokenKeyPrefix}:{jti}", cancellationToken);

            // Add to blacklist with remaining TTL
            var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (long.TryParse(expClaim, out var expUnix))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                var remainingTime = expiry - DateTime.UtcNow;

                if (remainingTime > TimeSpan.Zero)
                {
                    await BlacklistTokenAsync(jti, remainingTime, cancellationToken);
                }
            }

            _logger.LogInformation("Token revoked successfully. JTI: {Jti}", jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
        }
    }

    /// <inheritdoc />
    public async Task<TokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Get refresh token data from Redis
        var refreshTokenData = await _cacheService.GetAsync<RefreshTokenData>(
            $"{RefreshTokenKeyPrefix}:{refreshToken}",
            cancellationToken);

        if (refreshTokenData == null)
        {
            _logger.LogWarning("Refresh token not found in Redis");
            throw new UnauthorizedException("Invalid or expired refresh token");
        }

        // Check if refresh token has expired
        if (refreshTokenData.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token has expired");
            await _cacheService.RemoveAsync($"{RefreshTokenKeyPrefix}:{refreshToken}", cancellationToken);
            throw new UnauthorizedException("Refresh token has expired");
        }

        // Revoke old access token if it exists
        await _cacheService.RemoveAsync($"{TokenKeyPrefix}:{refreshTokenData.Jti}", cancellationToken);

        // Remove old refresh token (token rotation)
        await _cacheService.RemoveAsync($"{RefreshTokenKeyPrefix}:{refreshToken}", cancellationToken);

        // Generate new tokens
        var newTokenResult = await GenerateTokenAsync(
            refreshTokenData.UserId,
            refreshTokenData.Email,
            refreshTokenData.TenantId,
            refreshTokenData.Roles,
            cancellationToken);

        _logger.LogInformation(
            "Tokens refreshed for user {UserId} in tenant {TenantId}",
            refreshTokenData.UserId,
            refreshTokenData.TenantId);

        return newTokenResult;
    }

    /// <inheritdoc />
    public async Task BlacklistTokenAsync(string jti, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        await _cacheService.SetAsync(
            $"{BlacklistKeyPrefix}:{jti}",
            true,
            expiry,
            cancellationToken);

        _logger.LogDebug("Token blacklisted. JTI: {Jti}, Expiry: {Expiry}", jti, expiry);
    }

    /// <summary>
    /// Generates a JWT token with the specified claims.
    /// </summary>
    private string GenerateJwtToken(IEnumerable<Claim> claims, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    private ClaimsPrincipal? ValidateJwtToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return null;
        }
    }
}

/// <summary>
/// Data stored in Redis for access tokens.
/// </summary>
internal class TokenData
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Data stored in Redis for refresh tokens.
/// </summary>
internal class RefreshTokenData
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
