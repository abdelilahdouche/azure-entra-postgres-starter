using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StoreOps.Core;
using StoreOps.Core.Data;
using StoreOps.Jobs;

// Container Apps Jobs entry point. The same image runs different commands:
//   migrate  -> apply database migrations (manual job, started by the pipeline before smoke tests)
//   cleanup  -> delete reports older than the retention period (scheduled job, cron in Bicep)

var command = args.FirstOrDefault()?.ToLowerInvariant();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddStoreOpsCore(builder.Configuration);
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddSingleton<RetentionCleanup>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("StoreOps.Jobs");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    switch (command)
    {
        case "migrate":
            await host.Services.GetRequiredService<MigrationRunner>().ApplyAsync(cts.Token);
            break;
        case "cleanup":
            await host.Services.GetRequiredService<RetentionCleanup>().RunAsync(cts.Token);
            break;
        default:
            logger.LogError("Unknown command '{Command}'. Usage: StoreOps.Jobs <migrate|cleanup>", command);
            return 2;
    }

    return 0;
}
catch (Exception e)
{
    // A non-zero exit code marks the job execution as failed (visible in the portal and in alerts).
    logger.LogCritical(e, "Job '{Command}' failed", command);
    return 1;
}
