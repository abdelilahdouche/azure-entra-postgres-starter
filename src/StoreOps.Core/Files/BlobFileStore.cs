using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace StoreOps.Core.Files;

/// <summary>
/// Uploads stored in Azure Blob Storage and accessed with the managed identity
/// (shared-key access is disabled on the storage account).
/// </summary>
public sealed class BlobFileStore(BlobContainerClient container) : IFileStore
{
    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct = default) =>
        await container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);

    public async Task<Stream> OpenReadAsync(string name, CancellationToken ct = default) =>
        await container.GetBlobClient(name).OpenReadAsync(cancellationToken: ct);

    public async Task DeleteAsync(string name, CancellationToken ct = default) =>
        await container.GetBlobClient(name).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
}
