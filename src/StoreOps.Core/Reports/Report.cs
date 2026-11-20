namespace StoreOps.Core.Reports;

public enum ReportStatus { Submitted, Approved }

/// <summary>A daily store report (cash count, inventory sheet, incident...) uploaded by store staff.</summary>
public sealed record Report(
    Guid Id,
    string StoreCode,
    string Title,
    string FileName,
    string ContentType,
    string BlobName,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    ReportStatus Status = ReportStatus.Submitted,
    string? ApprovedBy = null,
    DateTimeOffset? ApprovedAt = null)
{
    public Report Approve(string approver, DateTimeOffset at)
    {
        if (Status == ReportStatus.Approved)
            throw new InvalidOperationException("Report is already approved.");
        if (string.Equals(approver, UploadedBy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A report cannot be approved by the person who uploaded it.");

        return this with { Status = ReportStatus.Approved, ApprovedBy = approver, ApprovedAt = at };
    }
}
