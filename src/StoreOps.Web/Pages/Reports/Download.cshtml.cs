using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreOps.Core.Files;
using StoreOps.Core.Reports;

namespace StoreOps.Web.Pages.Reports;

/// <summary>Files are streamed through the app (never exposed with public or SAS URLs), so page authorization applies.</summary>
public sealed class DownloadModel(IReportRepository reports, IFileStore files) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var report = await reports.GetAsync(id, ct);
        if (report is null) return NotFound();

        var stream = await files.OpenReadAsync(report.BlobName, ct);
        return File(stream, report.ContentType, report.FileName);
    }
}
