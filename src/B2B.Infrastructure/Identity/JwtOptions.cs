namespace B2B.Infrastructure.Identity;

/// <summary>
/// Configuration options for JWT authentication.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the secret key for signing tokens.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    public string Issuer { get; set; } = "B2B.API";

    /// <summary>
    /// Gets or sets the token audience.
    /// </summary>
    public string Audience { get; set; } = "B2B.Client";

    /// <summary>
    /// Gets or sets the access token expiration time in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the refresh token expiration time in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether to validate the issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the token lifetime.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the signing key.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Gets or sets the clock skew tolerance in minutes.
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 1;
}
