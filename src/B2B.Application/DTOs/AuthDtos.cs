namespace B2B.Application.DTOs;

/// <summary>
/// Request DTO for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for token refresh.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for user logout.
/// </summary>
public class LogoutRequest
{
    /// <summary>
    /// Gets or sets the access token to revoke.
    /// </summary>
    public string? AccessToken { get; set; }
}
