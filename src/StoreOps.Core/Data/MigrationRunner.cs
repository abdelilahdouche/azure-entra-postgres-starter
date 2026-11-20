using Microsoft.Extensions.Logging;
using Npgsql;

namespace StoreOps.Core.Data;

/// <summary>
/// Minimal, forward-only SQL migration runner. Scripts are embedded resources named
/// <c>NNN_description.sql</c> and are applied once, in order, each in its own transaction.
/// A PostgreSQL advisory lock makes concurrent runs safe.
/// </summary>
public sealed class MigrationRunner(NpgsqlDataSource dataSource, ILogger<MigrationRunner> logger)
{
    private const long AdvisoryLockKey = 7_421_001;
    private const string ResourcePrefix = "StoreOps.Core.Migrations.";

    public async Task<IReadOnlyList<string>> ApplyAsync(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await ExecuteAsync(connection, $"SELECT pg_advisory_lock({AdvisoryLockKey})", ct);
        try
        {
            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    id         text PRIMARY KEY,
                    applied_at timestamptz NOT NULL DEFAULT now()
                )
                """, ct);

            var applied = await GetAppliedAsync(connection, ct);
            var newlyApplied = new List<string>();

            foreach (var (id, sql) in LoadScripts())
            {
                if (applied.Contains(id)) continue;

                logger.LogInformation("Applying migration {MigrationId}", id);
                await using var tx = await connection.BeginTransactionAsync(ct);
                await ExecuteAsync(connection, sql, ct, tx);
                await using (var insert = new NpgsqlCommand("INSERT INTO schema_migrations (id) VALUES (@id)", connection, tx))
                {
                    insert.Parameters.AddWithValue("id", id);
                    await insert.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                newlyApplied.Add(id);
            }

            logger.LogInformation("Database is up to date ({Count} new migration(s))", newlyApplied.Count);
            return newlyApplied;
        }
        finally
        {
            await ExecuteAsync(connection, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
    }

    public static IEnumerable<(string Id, string Sql)> LoadScripts()
    {
        var assembly = typeof(MigrationRunner).Assembly;

        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return (resource[ResourcePrefix.Length..^".sql".Length], reader.ReadToEnd());
        }
    }

    private static async Task<HashSet<string>> GetAppliedAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand("SELECT id FROM schema_migrations", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct, NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
