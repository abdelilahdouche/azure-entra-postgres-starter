namespace StoreOps.Core.Files;

/// <summary>Development-only file store on local disk.</summary>
public sealed class LocalFileStore(string root) : IFileStore
{
    public async Task SaveAsync(string name, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenReadAsync(string name, CancellationToken ct = default) =>
        Task.FromResult<Stream>(File.OpenRead(PathFor(name)));

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        File.Delete(PathFor(name));
        return Task.CompletedTask;
    }

    private string PathFor(string name)
    {
        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(fullRoot, name));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid file name.", nameof(name));
        return path;
    }
}
