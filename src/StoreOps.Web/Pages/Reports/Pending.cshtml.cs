using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreOps.Core.Reports;
using StoreOps.Web.Authorization;

namespace StoreOps.Web.Pages.Reports;

public sealed class PendingModel(IReportRepository reports, TimeProvider clock) : PageModel
{
    public IReadOnlyList<Report> Reports { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Reports = await reports.ListAsync(ReportStatus.Submitted, ct: ct);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        var report = await reports.GetAsync(id, ct);
        if (report is null) return NotFound();

        try
        {
            await reports.UpdateAsync(report.Approve(User.GetUserName(), clock.GetUtcNow()), ct);
            TempData["Message"] = $"\"{report.Title}\" approved.";
        }
        catch (InvalidOperationException e)
        {
            TempData["Error"] = e.Message;
        }

        return RedirectToPage();
    }
}
