namespace DocumentAPI.Persistence;

using System.Data.Common;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Authenticates SQL Server connections with a Microsoft Entra access token so that no
/// credentials are stored in the connection string.
/// </summary>
/// <param name="credential">The token credential used to acquire access tokens.</param>
internal sealed class AzureSqlAuthenticationInterceptor(TokenCredential credential) : DbConnectionInterceptor
{
    private static readonly string[] Scopes = ["https://database.windows.net/.default"];

    private readonly TokenCredential _credential = credential;

    /// <inheritdoc />
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection is SqlConnection sqlConnection && string.IsNullOrEmpty(sqlConnection.AccessToken))
        {
            var token = _credential.GetToken(new TokenRequestContext(Scopes), CancellationToken.None);
            sqlConnection.AccessToken = token.Token;
        }

        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqlConnection sqlConnection && string.IsNullOrEmpty(sqlConnection.AccessToken))
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);
            sqlConnection.AccessToken = token.Token;
        }

        return result;
    }
}
