using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiUserSessionIntegrationTests {
    [Fact]
    public async Task Registered_administrator_manages_accounts_while_machine_and_user_credentials_cannot() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var administrator = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, administrator.Token);
        var user = await CreateUserAsync(host.Client, "Reader", password, [ApiAccessScopeNames.ReadWorkflows]);
        Assert.Equal("Reader", user.UserName);
        var session = await LoginAsync(host.Client, "rEaDeR", password);
        SetToken(host.Client, session.Token);
        using var me = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var identity = await me.Content.ReadFromJsonAsync<ApiSessionIdentity>();
        Assert.Equal(user.Id, identity!.UserId);
        Assert.False(identity.IsAdministrator);
        Assert.Equal(new[] { ApiAccessScopeNames.Session, ApiAccessScopeNames.ReadWorkflows }.Order(), identity.Scopes.Order());
        using var allowed = await host.Client.GetAsync("/api/workflows/contract");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var forbidden = await host.Client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var users = await host.Client.GetAsync("/api/access/users");
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        var machine = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = ApiBootstrapAdminOptions.Subject, DisplayName = "admin", Scopes = [ApiAccessScopeNames.Api, ApiAccessScopeNames.IssueTokens]
        });
        SetToken(host.Client, machine.Token);
        using var machineAdmin = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest());
        using var machineMe = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.Forbidden, machineAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, machineMe.StatusCode);
        using var broad = await host.Client.GetAsync("/api/workflows/contract");
        Assert.Equal(HttpStatusCode.OK, broad.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await host.Client.GetAsync("/api/access/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains(anonymous.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        Assert.Single(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
    }

    [Fact]
    public async Task Account_mutations_reject_old_sessions_without_revoking_machine_tokens() {
        var password = Password();
        var replacement = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "mutable", password, [ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.ReadProjects]);
        var old = await LoginAsync(host.Client, user.UserName, password);
        var machine = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() { Scopes = [ApiAccessScopeNames.ReadWorkflows] });
        user = await UpdateAsync(host.Client, user, true, [ApiAccessScopeNames.ReadProjects]);
        await AssertRejectedAsync(host.Client, old.Token);
        SetToken(host.Client, admin.Token);
        var current = await LoginAsync(host.Client, user.UserName, password);
        Assert.DoesNotContain(ApiAccessScopeNames.ReadWorkflows, current.Scopes);
        using var reset = await host.Client.PostAsJsonAsync($"/api/access/users/{user.Id}/reset-password", new ApiUserPasswordResetRequest(replacement, user.Version));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        user = (await reset.Content.ReadFromJsonAsync<ApiUserDetails>())!;
        await AssertRejectedAsync(host.Client, current.Token);
        using var wrong = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest(user.UserName, password));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var beforeDisable = await LoginAsync(host.Client, user.UserName, replacement);
        SetToken(host.Client, admin.Token);
        user = await UpdateAsync(host.Client, user, false, user.Scopes);
        await AssertRejectedAsync(host.Client, beforeDisable.Token);
        SetToken(host.Client, admin.Token);
        user = await UpdateAsync(host.Client, user, true, user.Scopes);
        await AssertRejectedAsync(host.Client, beforeDisable.Token);
        var beforeDelete = await LoginAsync(host.Client, user.UserName, replacement);
        SetToken(host.Client, admin.Token);
        using var deletion = await host.Client.DeleteAsync($"/api/access/users/{user.Id}?expectedVersion={user.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        var recreated = await CreateUserAsync(host.Client, user.UserName, replacement, []);
        Assert.NotEqual(user.Id, recreated.Id);
        await AssertRejectedAsync(host.Client, beforeDelete.Token);
        SetToken(host.Client, machine.Token);
        using var machineResponse = await host.Client.GetAsync("/api/workflows/contract");
        Assert.Equal(HttpStatusCode.OK, machineResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_only_current_session_and_empty_grants_never_become_business_access() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        await CreateUserAsync(host.Client, "empty", password, []);
        var first = await LoginAsync(host.Client, "empty", password);
        var second = await LoginAsync(host.Client, "empty", password);
        Assert.Equal([ApiAccessScopeNames.Session], first.Scopes);
        SetToken(host.Client, first.Token);
        using var business = await host.Client.GetAsync("/api/workflows/contract");
        Assert.Equal(HttpStatusCode.Forbidden, business.StatusCode);
        using var logout = await host.Client.PostAsync("/api/access/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertRejectedAsync(host.Client, first.Token);
        SetToken(host.Client, second.Token);
        using var other = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Overposting_conflicts_and_store_corruption_fail_without_writing_or_leaking_credentials() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        using var overpost = await host.Client.PostAsJsonAsync("/api/access/users", new {
            userName = "overposted", displayName = "Overposted", password, enabled = true, scopes = Array.Empty<string>(), isAdmin = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, overpost.StatusCode);
        Assert.Empty(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        var user = await CreateUserAsync(host.Client, "conflict", password, []);
        var updated = await UpdateAsync(host.Client, user, false, []);
        using var stale = await host.Client.PutAsJsonAsync($"/api/access/users/{user.Id}", new ApiUserUpdateRequest(user.UserName, "Stale", true, [ApiAccessScopeNames.ReadWorkflows], user.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var duplicate = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest("CONFLICT", "Duplicate", password, true, []));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var actual = Assert.Single(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        Assert.False(actual.Enabled);
        Assert.Equal(updated.Version, actual.Version);
        using var list = await host.Client.GetAsync("/api/access/users");
        var json = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.False(json.Contains(actual.PasswordHash, StringComparison.Ordinal), "The account list disclosed a password hash.");
        var root = host.App.Services.GetRequiredService<IControlPlanePathResolver>().ResolveRootPath();
        var path = Path.Combine(root, "api-users", "accounts.json");
        await File.WriteAllTextAsync(path, "{}");
        using var broken = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest("new", "New", password, true, []));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, broken.StatusCode);
        Assert.Equal("{}", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Management_disabled_removes_all_management_routes_even_for_a_registered_admin() {
        var password = Password();
        await using var host = await CreateHostAsync(password, management: false);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        using var me = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        foreach (var path in new[] { "/api/access/users", "/api/access/tokens", "/api/access/scopes" }) {
            using var response = await host.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
        using var issuance = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest());
        Assert.Equal(HttpStatusCode.NotFound, issuance.StatusCode);
        Assert.Empty(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
    }

    [Fact]
    public async Task Composed_endpoint_inventory_has_section_or_explicit_authority_on_every_business_route() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var routes = ((IEndpointRouteBuilder)host.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true).ToArray();
        Assert.NotEmpty(routes);
        var anonymous = new[] { "/api/access/status", "/api/access/login", "/api/plugins/oauth/callback" };
        foreach (var endpoint in routes) {
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) {
                Assert.Contains(endpoint.RoutePattern.RawText, anonymous);
                continue;
            }
            Assert.True(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Policy)),
                $"Unclassified business route: {endpoint.RoutePattern.RawText}");
        }
        Assert.Equal(routes.Length, routes.Select(endpoint => (endpoint.RoutePattern.RawText,
            string.Join(',', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods))).Distinct().Count());
    }

    internal static Task<ApiTestHost> CreateHostAsync(string password, bool management = true, Action<IServiceCollection>? configureServices = null) => ApiTestHost.CreateAsync(true, configureServices,
        useInMemoryDatabase: true, apiConfiguration: new Dictionary<string, string?> {
            ["Api:UserAuthentication:Enabled"] = "true",
            ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
            ["Api:AccessManagement:Enabled"] = management.ToString(),
            ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
        });

    internal static string Password() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    internal static void SetToken(HttpClient client, string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    internal static async Task<ApiLoginResult> LoginAsync(HttpClient client, string userName, string password) {
        using var response = await client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<ApiLoginResult>())!;
    }

    internal static async Task<ApiUserDetails> CreateUserAsync(HttpClient client, string userName, string password, IReadOnlyList<string> scopes) {
        using var response = await client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest(userName, userName, password, true, scopes));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiUserDetails>())!;
    }

    internal static async Task<ApiUserDetails> UpdateAsync(HttpClient client, ApiUserDetails user, bool enabled, IReadOnlyList<string> scopes) {
        using var response = await client.PutAsJsonAsync($"/api/access/users/{user.Id}", new ApiUserUpdateRequest(user.UserName, user.DisplayName, enabled, scopes, user.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiUserDetails>())!;
    }

    internal static async Task AssertRejectedAsync(HttpClient client, string token) {
        SetToken(client, token);
        using var response = await client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
