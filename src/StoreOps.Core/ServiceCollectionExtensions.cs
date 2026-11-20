using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StoreOps.Core.Data;
using StoreOps.Core.Files;
using StoreOps.Core.Reports;

namespace StoreOps.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers data access and file storage. In Azure everything authenticates with the
    /// user-assigned managed identity (AZURE_CLIENT_ID env var); locally DefaultAzureCredential
    /// falls back to your Azure CLI / Visual Studio login.
    /// </summary>
    public static IServiceCollection AddStoreOpsCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddSingleton(sp => PostgresDataSourceFactory.Create(
            sp.GetRequiredService<IOptions<PostgresOptions>>().Value,
            sp.GetRequiredService<TokenCredential>()));

        services.AddSingleton<IFileStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BlobEndpoint))
                return new LocalFileStore(options.LocalPath);

            var container = new BlobServiceClient(new Uri(options.BlobEndpoint), sp.GetRequiredService<TokenCredential>())
                .GetBlobContainerClient(options.UploadsContainer);
            return new BlobFileStore(container);
        });

        services.AddSingleton<IReportRepository, PostgresReportRepository>();
        services.AddSingleton<ISystemStatus, PostgresSystemStatus>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
