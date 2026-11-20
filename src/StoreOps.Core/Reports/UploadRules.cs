namespace StoreOps.Core.Reports;

public static class UploadRules
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
    };

    /// <returns>An error message, or <c>null</c> when the file is accepted.</returns>
    public static string? Validate(string fileName, long length)
    {
        if (length <= 0) return "The file is empty.";
        if (length > MaxBytes) return $"The file is larger than {MaxBytes / 1024 / 1024} MB.";
        if (!AllowedTypes.ContainsKey(Path.GetExtension(fileName)))
            return $"Allowed file types: {string.Join(", ", AllowedTypes.Keys)}.";
        return null;
    }

    /// <summary>The content type is derived from the extension, never trusted from the browser.</summary>
    public static string ContentTypeFor(string fileName) =>
        AllowedTypes.TryGetValue(Path.GetExtension(fileName), out var type) ? type : "application/octet-stream";

    /// <summary>Blob names never contain user input (no path traversal, no PII in URLs).</summary>
    public static string BlobNameFor(Guid reportId, string fileName, DateTimeOffset at) =>
        $"{at:yyyy}/{at:MM}/{reportId:N}{Path.GetExtension(fileName).ToLowerInvariant()}";
}
