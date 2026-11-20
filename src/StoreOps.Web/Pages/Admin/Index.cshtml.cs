using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using StoreOps.Core.Data;
using StoreOps.Core.Files;

namespace StoreOps.Web.Pages.Admin;

public sealed class IndexModel(
    ISystemStatus status,
    IOptions<PostgresOptions> postgres,
    IOptions<StorageOptions> storage,
    IWebHostEnvironment environment,
    IConfiguration configuration) : PageModel
{
    public DatabaseStatus Database { get; private set; } = null!;
    public PostgresOptions Postgres => postgres.Value;
    public StorageOptions Storage => storage.Value;
    public string EnvironmentName => environment.EnvironmentName;
    public string Version => configuration["APP_VERSION"] ?? "local";

    public async Task OnGetAsync(CancellationToken ct) => Database = await status.GetDatabaseStatusAsync(ct);
}
