using Npgsql;

namespace StoreOps.Core.Data;

public sealed record DatabaseStatus(
    bool CanConnect,
    string? CurrentUser,
    string? ServerVersion,
    IReadOnlyList<string> AppliedMigrations,
    string? Error);

public interface ISystemStatus
{
    Task<DatabaseStatus> GetDatabaseStatusAsync(CancellationToken ct = default);
}

public sealed class PostgresSystemStatus(NpgsqlDataSource dataSource) : ISystemStatus
{
    public async Task<DatabaseStatus> GetDatabaseStatusAsync(CancellationToken ct = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);

            string? user = null, version = null;
            await using (var cmd = new NpgsqlCommand("SELECT current_user, version()", connection))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    user = reader.GetString(0);
                    version = reader.GetString(1);
                }
            }

            var migrations = new List<string>();
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT id FROM schema_migrations ORDER BY id", connection);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) migrations.Add(reader.GetString(0));
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // Migrations have not run yet.
            }

            return new DatabaseStatus(true, user, version, migrations, null);
        }
        catch (Exception e) when (e is NpgsqlException or Azure.Identity.AuthenticationFailedException or TimeoutException)
        {
            return new DatabaseStatus(false, null, null, [], e.Message);
        }
    }
}
