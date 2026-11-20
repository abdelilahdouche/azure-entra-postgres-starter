namespace StoreOps.Core.Reports;

public interface IReportRepository
{
    Task<IReadOnlyList<Report>> ListAsync(ReportStatus? status = null, int limit = 100, CancellationToken ct = default);
    Task<Report?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task UpdateAsync(Report report, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> ListOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
