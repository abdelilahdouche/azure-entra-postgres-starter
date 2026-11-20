using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StoreOps.Core.Files;
using StoreOps.Core.Reports;
using StoreOps.Web.Authorization;

namespace StoreOps.Web.Pages.Reports;

public sealed class IndexModel(IReportRepository reports, IFileStore files, TimeProvider clock) : PageModel
{
    public IReadOnlyList<Report> Reports { get; private set; } = [];

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => Reports = await reports.ListAsync(limit: 50, ct: ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input.File is { } file && UploadRules.Validate(file.FileName, file.Length) is { } error)
            ModelState.AddModelError("Input.File", error);

        if (!ModelState.IsValid || Input.File is null)
        {
            Reports = await reports.ListAsync(limit: 50, ct: ct);
            return Page();
        }

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var fileName = Path.GetFileName(Input.File.FileName);
        var contentType = UploadRules.ContentTypeFor(fileName);
        var blobName = UploadRules.BlobNameFor(id, fileName, now);

        await using (var stream = Input.File.OpenReadStream())
        {
            await files.SaveAsync(blobName, stream, contentType, ct);
        }

        await reports.AddAsync(new Report(
            id, Input.StoreCode.Trim().ToUpperInvariant(), Input.Title.Trim(), fileName, contentType,
            blobName, User.GetUserName(), now), ct);

        TempData["Message"] = $"Report \"{Input.Title}\" uploaded.";
        return RedirectToPage();
    }

    public sealed class UploadInput
    {
        [Required, RegularExpression("^[A-Za-z]{2,4}-[0-9]{2,4}$", ErrorMessage = "Store code format: ABC-123.")]
        public string StoreCode { get; set; } = "";

        [Required, StringLength(120)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Choose a file.")]
        public IFormFile? File { get; set; }
    }
}
