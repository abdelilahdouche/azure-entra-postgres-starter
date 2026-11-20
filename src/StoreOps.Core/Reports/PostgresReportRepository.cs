using Npgsql;

namespace StoreOps.Core.Reports;

public sealed class PostgresReportRepository(NpgsqlDataSource dataSource) : IReportRepository
{
    private const string Columns =
        "id, store_code, title, file_name, content_type, blob_name, uploaded_by, uploaded_at, status, approved_by, approved_at";

    public async Task<IReadOnlyList<Report>> ListAsync(ReportStatus? status = null, int limit = 100, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand($"""
            SELECT {Columns} FROM reports
            WHERE (@status::text IS NULL OR status = @status::text)
            ORDER BY uploaded_at DESC
            LIMIT @limit
            """);
        cmd.Parameters.AddWithValue("status", (object?)status?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<Report?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand($"SELECT {Columns} FROM reports WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);
        return (await ReadAllAsync(cmd, ct)).SingleOrDefault();
    }

    public async Task AddAsync(Report r, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand($"""
            INSERT INTO reports ({Columns})
            VALUES (@id, @store_code, @title, @file_name, @content_type, @blob_name,
                    @uploaded_by, @uploaded_at, @status, @approved_by, @approved_at)
            """);
        cmd.Parameters.AddWithValue("id", r.Id);
        cmd.Parameters.AddWithValue("store_code", r.StoreCode);
        cmd.Parameters.AddWithValue("title", r.Title);
        cmd.Parameters.AddWithValue("file_name", r.FileName);
        cmd.Parameters.AddWithValue("content_type", r.ContentType);
        cmd.Parameters.AddWithValue("blob_name", r.BlobName);
        cmd.Parameters.AddWithValue("uploaded_by", r.UploadedBy);
        cmd.Parameters.AddWithValue("uploaded_at", r.UploadedAt);
        AddStatusParameters(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Report r, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            UPDATE reports
            SET status = @status, approved_by = @approved_by, approved_at = @approved_at
            WHERE id = @id
            """);
        cmd.Parameters.AddWithValue("id", r.Id);
        AddStatusParameters(cmd, r);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Report>> ListOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand($"SELECT {Columns} FROM reports WHERE uploaded_at < @cutoff");
        cmd.Parameters.AddWithValue("cutoff", cutoff);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("DELETE FROM reports WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddStatusParameters(NpgsqlCommand cmd, Report r)
    {
        cmd.Parameters.AddWithValue("status", r.Status.ToString());
        cmd.Parameters.AddWithValue("approved_by", (object?)r.ApprovedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("approved_at", (object?)r.ApprovedAt ?? DBNull.Value);
    }

    private static async Task<IReadOnlyList<Report>> ReadAllAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Report>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Report(
                Id: reader.GetGuid(0),
                StoreCode: reader.GetString(1),
                Title: reader.GetString(2),
                FileName: reader.GetString(3),
                ContentType: reader.GetString(4),
                BlobName: reader.GetString(5),
                UploadedBy: reader.GetString(6),
                UploadedAt: reader.GetFieldValue<DateTimeOffset>(7),
                Status: Enum.Parse<ReportStatus>(reader.GetString(8)),
                ApprovedBy: reader.IsDBNull(9) ? null : reader.GetString(9),
                ApprovedAt: reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10)));
        }
        return list;
    }
}
