namespace DocumentAPI.Options;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Describes the JWT bearer authentication settings used by the API.
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>
    /// Gets or sets the expected token issuer.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected token audience.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the symmetric signing key used to validate bearer tokens.
    /// </summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS is required for the metadata address or authority.
    /// Defaults to <see langword="true"/>. Set to <see langword="false"/> only in local development scenarios.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}