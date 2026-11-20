using Azure.Core;
using Npgsql;

namespace StoreOps.Core.Data;

public static class PostgresDataSourceFactory
{
    // Audience for Azure Database for PostgreSQL Entra tokens.
    private static readonly TokenRequestContext TokenRequest =
        new(["https://ossrdbms-aad.database.windows.net/.default"]);

    public static NpgsqlDataSource Create(PostgresOptions options, TokenCredential credential)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            SslMode = options.UseEntraAuth ? SslMode.Require : SslMode.Prefer,
            Password = options.UseEntraAuth ? null : options.Password,
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        if (options.UseEntraAuth)
        {
            // Entra tokens live ~60-90 minutes. Npgsql refreshes the "password" in the background,
            // so every new physical connection uses a valid token. Nothing is stored.
            builder.UsePeriodicPasswordProvider(
                async (_, ct) => (await credential.GetTokenAsync(TokenRequest, ct)).Token,
                successRefreshInterval: TimeSpan.FromMinutes(30),
                failureRefreshInterval: TimeSpan.FromSeconds(10));
        }

        return builder.Build();
    }
}
