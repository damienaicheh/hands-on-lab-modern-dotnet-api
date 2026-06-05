namespace DocumentAPI.Services.Identity;

using Azure.Core;
using Azure.Identity;

/// <summary>
/// Creates Azure credentials that authenticate with the current app or developer identity.
/// </summary>
internal static class AzureIdentityCredentialFactory
{
    /// <summary>
    /// Creates a token credential for the configured managed identity or the default developer/app identity chain.
    /// </summary>
    /// <param name="managedIdentityClientId">The optional user-assigned managed identity client identifier.</param>
    /// <returns>A credential that authenticates using Microsoft Entra ID.</returns>
    public static TokenCredential Create(string? managedIdentityClientId)
    {
        return string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = managedIdentityClientId,
            });
    }
}