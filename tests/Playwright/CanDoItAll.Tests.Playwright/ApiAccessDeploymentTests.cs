using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using static CanDoItAll.Tests.Playwright.ApiAccessSettingsBrowserTests;

namespace CanDoItAll.Tests.Playwright;

public sealed class ApiAccessDeploymentTests {
    [Fact]
    public async Task Production_TLS_CORS_proxy_isolation_admin_rotation_and_failure_pipeline_are_enforced() {
        await using var host = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await host.StartAsync(password, loopbackHttp: false);
        using var cleartext = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var spoofed = new HttpRequestMessage(HttpMethod.Post, host.HttpUrl + "/api/access/login") {
            Content = JsonContent.Create(new ApiLoginRequest("admin", password))
        };
        spoofed.Headers.Add("X-Forwarded-Proto", "https");
        spoofed.Headers.Add("X-Forwarded-For", "127.0.0.1");
        using var spoofedResponse = await cleartext.SendAsync(spoofed);
        Assert.Equal(HttpStatusCode.BadRequest, spoofedResponse.StatusCode);
        Assert.Equal("application/json", spoofedResponse.Content.Headers.ContentType?.MediaType);
        using var oversized = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("admin", new string('x', 17000)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal("application/json", oversized.Content.Headers.ContentType?.MediaType);
        Assert.True(oversized.Headers.CacheControl?.NoStore);
        var admin = await LoginAsync(host.Client, "admin", password);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", admin.Token);
        using var issued = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest {
            Subject = "rotation-independent-machine", Scopes = [ApiAccessScopeNames.ReadWorkflows]
        });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var machine = (await issued.Content.ReadFromJsonAsync<ApiTokenIssueResult>())!;
        using var createdUser = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest("production-business",
            "Production business", password, true, [ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows, ApiAccessScopeNames.ReadProcesses]));
        Assert.Equal(HttpStatusCode.OK, createdUser.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        var business = await LoginAsync(host.Client, "production-business", password);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", business.Token);
        var definitions = (await host.Client.GetFromJsonAsync<JsonObject>("/api/processes/definitions"))!["items"]!.AsArray();
        Assert.NotEmpty(definitions);
        var keyNode = definitions[0]!["key"]!;
        var key = keyNode is JsonObject keyObject ? keyObject["value"]!.GetValue<string>() : keyNode.GetValue<string>();
        foreach (var suffix in new[] { "", "/roles", "/steps" }) {
            using var definition = await host.Client.GetAsync("/api/processes/definitions/" + Uri.EscapeDataString(key) + suffix);
            Assert.Equal(HttpStatusCode.OK, definition.StatusCode);
            Assert.NotEmpty((await definition.Content.ReadFromJsonAsync<JsonObject>())!);
        }
        var templates = (await host.Client.GetFromJsonAsync<JsonArray>("/api/workflows/templates"))!;
        Assert.NotEmpty(templates);
        var draftPath = "/api/workflows/templates/" + Uri.EscapeDataString(templates[0]!["key"]!.GetValue<string>()) + "/drafts";
        var drafts = await Task.WhenAll(host.Client.PostAsync(draftPath, null), host.Client.PostAsync(draftPath, null));
        var identities = new HashSet<WorkflowId>();
        foreach (var draft in drafts) {
            using (draft) {
                Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
                var persisted = (await draft.Content.ReadFromJsonAsync<WorkflowDefinition>())!;
                Assert.True(identities.Add(persisted.Id), "Concurrent draft requests reused a workflow identity.");
                Assert.Equal(WorkflowLifecycleStatus.Draft, persisted.Status);
                var storedDraft = (await host.Client.GetFromJsonAsync<WorkflowDefinitionDetail>($"/api/workflows/definitions/{persisted.Id.Value:D}"))!;
                Assert.Equal(persisted.Id, storedDraft.Definition.Id);
                Assert.Equal(WorkflowLifecycleStatus.Draft, storedDraft.Definition.Status);
            }
        }
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var absent = await host.Client.GetAsync("/api/absent-route");
        using var anonymous = await host.Client.GetAsync("/api/access/users");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("application/json", absent.Content.Headers.ContentType?.MediaType);
        foreach (var origin in new[] { "https://allowed.example", "https://denied.example" }) {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/access/login");
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
            using var response = await host.Client.SendAsync(preflight);
            if (origin == "https://allowed.example") {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            } else {
                Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            }
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
        var replacement = ApiAccessProductionHost.Secret();
        await host.StartAsync(replacement, loopbackHttp: false, overrides: new Dictionary<string, string?> {
            ["WebHost:TrustedProxies:0"] = "127.0.0.1"
        });
        await AssertStatusAsync(host.Client, admin.Token, "/api/access/me", HttpStatusCode.Unauthorized);
        await AssertStatusAsync(host.Client, machine.Token, "/api/workflows/templates", HttpStatusCode.OK);
        using var oldPassword = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("admin", password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var newAdmin = await LoginAsync(host.Client, "admin", replacement);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0,
            endpoint => endpoint.UseHttps(host.CertificatePath)));
        builder.Services.AddReverseProxy().LoadFromMemory(
            [new RouteConfig { RouteId = "approved-api", ClusterId = "private-backend", Match = new() { Path = "/api/{**remainder}" },
                Transforms = [new Dictionary<string, string> { ["X-Forwarded"] = "Set" }] }],
            [new ClusterConfig { ClusterId = "private-backend", Destinations = new Dictionary<string, DestinationConfig> {
                ["loopback"] = new() { Address = host.HttpUrl }
            } }]);
        await using var proxy = builder.Build();
        proxy.MapReverseProxy();
        await proxy.StartAsync();
        var proxyUrl = proxy.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        foreach (var path in new[] { "/", "/settings", "/settings?tab=api-access", "/_blazor/negotiate", "/_dev/status", "/authorized-files/content", "/managed-files/example", "/swagger", "/openapi/v1.json" }) {
            using var response = await host.Client.GetAsync(proxyUrl + path);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"Proxy unexpectedly exposed {path}.");
        }
        await AssertStatusAsync(host.Client, machine.Token, proxyUrl + "/api/workflows/templates", HttpStatusCode.OK);
        await AssertStatusAsync(host.Client, machine.Token, proxyUrl + "/api/access/users", HttpStatusCode.Forbidden);
        await AssertStatusAsync(host.Client, newAdmin.Token, proxyUrl + "/api/access/users", HttpStatusCode.OK);
        using var forwardedAnonymous = await host.Client.GetAsync(proxyUrl + "/api/access/users");
        Assert.Equal(HttpStatusCode.Unauthorized, forwardedAnonymous.StatusCode);
        foreach (var url in new[] { host.HttpUrl, host.HttpsUrl }) {
            var port = new Uri(url).Port;
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Where(endpoint => endpoint.Port == port).ToArray();
            Assert.NotEmpty(listeners);
            Assert.All(listeners, endpoint => Assert.True(IPAddress.IsLoopback(endpoint.Address), "The backend must not listen on an externally reachable address."));
        }
        await File.WriteAllTextAsync(Path.Combine(host.ControlPlaneRoot, "api-users", "accounts.json"), "{\"schemaVersion\":99,\"users\":[]}");
        await AssertStatusAsync(host.Client, newAdmin.Token, proxyUrl + "/api/access/users", HttpStatusCode.ServiceUnavailable);
        await proxy.StopAsync();
    }
}
