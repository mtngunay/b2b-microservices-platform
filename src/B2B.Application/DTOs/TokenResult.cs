namespace B2B.Application.DTOs;

/// <summary>
/// Represents the result of a token generation operation.
/// </summary>
public class TokenResult
{
    /// <summary>
    /// Gets or sets the JWT access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token for obtaining new access tokens.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type (typically "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the access token expiration time in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the access token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Creates a new TokenResult instance.
    /// </summary>
    /// <param name="accessToken">The JWT access token.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="expiresIn">The expiration time in seconds.</param>
    /// <returns>A new TokenResult instance.</returns>
    public static TokenResult Create(string accessToken, string refreshToken, int expiresIn)
    {
        return new TokenResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
        };
    }
}
