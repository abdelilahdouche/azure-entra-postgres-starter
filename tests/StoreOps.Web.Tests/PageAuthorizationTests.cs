using System.Net;
using StoreOps.Web.Authorization;
using StoreOps.Web.Tests.Infrastructure;

namespace StoreOps.Web.Tests;

/// <summary>
/// The access matrix: which Entra app role can open which page.
/// If someone changes a policy or a folder convention by mistake, these tests fail.
/// </summary>
public sealed class PageAuthorizationTests(StoreOpsWebFactory factory) : IClassFixture<StoreOpsWebFactory>
{
    public static TheoryData<string, string?, HttpStatusCode> AccessMatrix => new()
    {
        // path                 role (null = no role)  expected
        { "/",                  null,                  HttpStatusCode.OK },
        { "/Reports",           null,                  HttpStatusCode.Forbidden },
        { "/Reports",           AppRoles.Staff,        HttpStatusCode.OK },
        { "/Reports",           AppRoles.Manager,      HttpStatusCode.OK },
        { "/Reports/Pending",   AppRoles.Staff,        HttpStatusCode.Forbidden },
        { "/Reports/Pending",   AppRoles.Manager,      HttpStatusCode.OK },
        { "/Reports/Pending",   AppRoles.Admin,        HttpStatusCode.OK },
        { "/Admin",             AppRoles.Staff,        HttpStatusCode.Forbidden },
        { "/Admin",             AppRoles.Manager,      HttpStatusCode.Forbidden },
        { "/Admin",             AppRoles.Admin,        HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(AccessMatrix))]
    public async Task Signed_in_user_gets_expected_status(string path, string? role, HttpStatusCode expected)
    {
        var client = role is null ? factory.CreateClientAs("no.role@contoso.com") : factory.CreateClientAs("user@contoso.com", role);

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Reports")]
    [InlineData("/Admin")]
    public async Task Anonymous_user_is_challenged(string path)
    {
        var response = await factory.CreateClientAs(null).GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Liveness_probe_is_anonymous()
    {
        var response = await factory.CreateClientAs(null).GetAsync("/healthz/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Navigation_only_shows_links_the_user_can_open()
    {
        var html = await factory.CreateClientAs("staff@contoso.com", AppRoles.Staff).GetStringAsync("/");

        Assert.Contains("href=\"/Reports\"", html);
        Assert.DoesNotContain("href=\"/Reports/Pending\"", html);
        Assert.DoesNotContain("href=\"/Admin\"", html);
    }
}
