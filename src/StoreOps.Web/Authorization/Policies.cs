using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace StoreOps.Web.Authorization;

/// <summary>
/// App role values. They must match <c>scripts/entra/app-roles.json</c> (the app registration manifest).
/// Entra groups are assigned to these roles on the Enterprise Application, so access is managed
/// by group membership — no code change, no redeploy.
/// </summary>
public static class AppRoles
{
    public const string Staff = "Store.Staff";
    public const string Manager = "Store.Manager";
    public const string Admin = "StoreOps.Admin";
}

public static class Policies
{
    public const string CanViewReports = nameof(CanViewReports);
    public const string CanApproveReports = nameof(CanApproveReports);
    public const string IsAdmin = nameof(IsAdmin);

    public static AuthorizationOptions AddStoreOpsPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(CanViewReports, p => p.RequireRole(AppRoles.Staff, AppRoles.Manager, AppRoles.Admin));
        options.AddPolicy(CanApproveReports, p => p.RequireRole(AppRoles.Manager, AppRoles.Admin));
        options.AddPolicy(IsAdmin, p => p.RequireRole(AppRoles.Admin));
        return options;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string GetUserName(this ClaimsPrincipal user) =>
        user.FindFirstValue("preferred_username") ?? user.Identity?.Name ?? "unknown";

    public static IReadOnlyList<string> GetAppRoles(this ClaimsPrincipal user) =>
        user.Identities
            .SelectMany(i => i.FindAll(i.RoleClaimType))
            .Select(c => c.Value)
            .Distinct()
            .Order()
            .ToList();
}
