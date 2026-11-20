namespace StoreOps.Core.Files;

public interface IFileStore
{
    Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>e.g. https://stxxx.blob.core.windows.net/ — when empty, files go to local disk (development).</summary>
    public string? BlobEndpoint { get; set; }
    public string UploadsContainer { get; set; } = "uploads";
    public string LocalPath { get; set; } = "App_Data/uploads";
}
