using System.Collections.Concurrent;
using StoreOps.Core.Data;
using StoreOps.Core.Files;
using StoreOps.Core.Reports;

namespace StoreOps.Web.Tests.Infrastructure;

public sealed class InMemoryReportRepository : IReportRepository
{
    private readonly ConcurrentDictionary<Guid, Report> _reports = new();

    public Task<IReadOnlyList<Report>> ListAsync(ReportStatus? status = null, int limit = 100, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Report>>(_reports.Values
            .Where(r => status is null || r.Status == status)
            .OrderByDescending(r => r.UploadedAt)
            .Take(limit)
            .ToList());

    public Task<Report?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_reports.GetValueOrDefault(id));

    public Task AddAsync(Report report, CancellationToken ct = default)
    {
        _reports[report.Id] = report;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Report report, CancellationToken ct = default) => AddAsync(report, ct);

    public Task<IReadOnlyList<Report>> ListOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Report>>(_reports.Values.Where(r => r.UploadedAt < cutoff).ToList());

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _reports.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryFileStore : IFileStore
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Files[name] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string name, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream(Files[name]));

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        Files.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}

public sealed class FakeSystemStatus : ISystemStatus
{
    public Task<DatabaseStatus> GetDatabaseStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(new DatabaseStatus(true, "id-storeops-test", "PostgreSQL 16 (fake)", ["001_create_reports"], null));
}
