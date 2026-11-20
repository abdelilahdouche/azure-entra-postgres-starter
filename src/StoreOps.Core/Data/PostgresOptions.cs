namespace StoreOps.Core.Data;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "storeops";

    /// <summary>
    /// With Entra auth this is the name of the PostgreSQL role mapped to the managed identity
    /// (by default, the managed identity's name).
    /// </summary>
    public string Username { get; set; } = "postgres";

    /// <summary>
    /// When true, the connection password is a short-lived Entra access token obtained from the
    /// managed identity. No password is stored anywhere.
    /// </summary>
    public bool UseEntraAuth { get; set; }

    /// <summary>Local development only (docker compose). Ignored when <see cref="UseEntraAuth"/> is true.</summary>
    public string? Password { get; set; }
}
