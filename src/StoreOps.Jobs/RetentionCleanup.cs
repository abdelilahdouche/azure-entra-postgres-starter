using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoreOps.Core.Files;
using StoreOps.Core.Reports;

namespace StoreOps.Jobs;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Reports (and their files) older than this are deleted.</summary>
    public int Days { get; set; } = 400;
}

public sealed class RetentionCleanup(
    IReportRepository reports,
    IFileStore files,
    TimeProvider clock,
    IOptions<RetentionOptions> options,
    ILogger<RetentionCleanup> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.Days);
        var expired = await reports.ListOlderThanAsync(cutoff, ct);

        foreach (var report in expired)
        {
            // File first: if the job stops halfway, the row is still there and the next run retries.
            await files.DeleteAsync(report.BlobName, ct);
            await reports.DeleteAsync(report.Id, ct);
        }

        logger.LogInformation("Retention cleanup: {Count} report(s) older than {Cutoff:yyyy-MM-dd} deleted", expired.Count, cutoff);
        return expired.Count;
    }
}
