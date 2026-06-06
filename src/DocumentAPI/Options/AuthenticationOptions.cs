namespace DocumentAPI.Options;

/// <summary>
/// Describes the JWT bearer authentication settings used by the API.
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets the expected token issuer.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected token audience.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the symmetric signing key used to validate bearer tokens.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}