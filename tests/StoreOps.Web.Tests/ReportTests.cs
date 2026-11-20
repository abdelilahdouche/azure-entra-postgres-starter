using StoreOps.Core.Data;
using StoreOps.Core.Reports;

namespace StoreOps.Web.Tests;

public sealed class ReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);

    private static Report NewReport(string uploadedBy = "staff@contoso.com") =>
        new(Guid.NewGuid(), "MTL-012", "Cash count", "count.pdf", "application/pdf", "2026/11/x.pdf", uploadedBy, Now);

    [Fact]
    public void Manager_can_approve_a_submitted_report()
    {
        var approved = NewReport().Approve("manager@contoso.com", Now.AddHours(1));

        Assert.Equal(ReportStatus.Approved, approved.Status);
        Assert.Equal("manager@contoso.com", approved.ApprovedBy);
        Assert.Equal(Now.AddHours(1), approved.ApprovedAt);
    }

    [Fact]
    public void Uploader_cannot_approve_their_own_report()
    {
        var report = NewReport(uploadedBy: "manager@contoso.com");

        Assert.Throws<InvalidOperationException>(() => report.Approve("MANAGER@contoso.com", Now));
    }

    [Fact]
    public void A_report_cannot_be_approved_twice()
    {
        var approved = NewReport().Approve("manager@contoso.com", Now);

        Assert.Throws<InvalidOperationException>(() => approved.Approve("admin@contoso.com", Now));
    }

    [Theory]
    [InlineData("count.pdf", 1024, null)]
    [InlineData("photo.JPG", 1024, null)]
    [InlineData("empty.pdf", 0, "The file is empty.")]
    [InlineData("big.pdf", UploadRules.MaxBytes + 1, "The file is larger than 10 MB.")]
    public void Upload_rules_validate_size_and_type(string fileName, long length, string? expectedError)
    {
        Assert.Equal(expectedError, UploadRules.Validate(fileName, length));
    }

    [Theory]
    [InlineData("script.exe")]
    [InlineData("page.html")]
    [InlineData("noextension")]
    public void Upload_rules_reject_unexpected_file_types(string fileName)
    {
        Assert.NotNull(UploadRules.Validate(fileName, 1024));
    }

    [Fact]
    public void Blob_name_never_contains_user_input()
    {
        var id = Guid.Parse("0193f9a1-0000-7000-8000-000000000001");

        var name = UploadRules.BlobNameFor(id, "../../etc/passwd .PDF", Now);

        Assert.Equal("2026/11/0193f9a1000070008000000000000001.pdf", name);
    }

    [Fact]
    public void Migrations_are_embedded_and_ordered()
    {
        var ids = MigrationRunner.LoadScripts().Select(s => s.Id).ToList();

        Assert.Contains("001_create_reports", ids);
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
    }
}
