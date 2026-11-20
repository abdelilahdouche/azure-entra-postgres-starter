using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using StoreOps.Core;
using StoreOps.Core.Data;
using StoreOps.Core.Reports;
using StoreOps.Web.Authorization;

var builder = WebApplication.CreateBuilder(args);

// One credential for the whole app: the user-assigned managed identity in Azure
// (selected through AZURE_CLIENT_ID), your developer login locally.
TokenCredential credential = new DefaultAzureCredential();
builder.Services.AddSingleton(credential);
builder.Services.AddStoreOpsCore(builder.Configuration);

// ---- Authentication: Microsoft Entra ID (OpenID Connect) -------------------------------------
// In Azure, the app proves its identity to Entra with a federated credential backed by the
// managed identity: no client secret. See AzureAd__ClientCredentials__* in infra/modules/container-apps.bicep.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// App roles defined on the app registration arrive in the "roles" claim.
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
    options => options.TokenValidationParameters.RoleClaimType = "roles");

// ---- Authorization: Entra app roles -> policies -> pages ------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddStoreOpsPolicies();
    options.FallbackPolicy = options.DefaultPolicy; // every endpoint requires sign-in unless marked anonymous
});

builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Reports", Policies.CanViewReports);
        options.Conventions.AuthorizePage("/Reports/Pending", Policies.CanApproveReports);
        options.Conventions.AuthorizeFolder("/Admin", Policies.IsAdmin);
        options.Conventions.AllowAnonymousToPage("/Error");
    })
    .AddMicrosoftIdentityUI();

// ---- Data protection: keys shared by all replicas, stored in Blob, wrapped by Key Vault -----
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("storeops");
var keysBlobUri = builder.Configuration["DataProtection:BlobUri"];
var keyVaultKeyUri = builder.Configuration["DataProtection:KeyUri"];
if (!string.IsNullOrWhiteSpace(keysBlobUri) && !string.IsNullOrWhiteSpace(keyVaultKeyUri))
{
    dataProtection
        .PersistKeysToAzureBlobStorage(new Uri(keysBlobUri), credential)
        .ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), credential);
}

// ---- Observability: OpenTelemetry -> Application Insights ------------------------------------
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = UploadRules.MaxBytes + 1024 * 1024);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Microsoft.Identity.Web.UI sign-in / sign-out endpoints

// Liveness: the process is up. Readiness: the database is reachable with the managed identity.
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

app.Run();
