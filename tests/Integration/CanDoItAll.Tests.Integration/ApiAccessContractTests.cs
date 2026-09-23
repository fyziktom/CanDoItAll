using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiAccessContractTests {
    [Fact]
    public async Task OpenApi_uses_actual_route_gates_unique_operations_and_bearer_metadata_without_hashes() {
        var password = Password();
        foreach (var management in new[] { false, true }) {
            await using var host = await CreateHostAsync(password, management);
            var document = (await host.Client.GetFromJsonAsync<JsonObject>("/openapi/v1.json"))!;
            var paths = document["paths"]!.AsObject();
            Assert.Equal(management, paths.ContainsKey("/api/access/tokens"));
            Assert.Equal(management, paths.ContainsKey("/api/access/users"));
            Assert.Null(paths["/api/access/login"]!["post"]!["security"]);
            Assert.NotNull(paths["/api/access/me"]!["get"]!["security"]);
            var bearer = document["components"]!["securitySchemes"]!["Bearer"]!;
            Assert.Equal("http", bearer["type"]!.GetValue<string>());
            Assert.Equal("bearer", bearer["scheme"]!.GetValue<string>());
            Assert.Equal("JWT", bearer["bearerFormat"]!.GetValue<string>());
            Assert.NotNull(paths["/api/access/me"]!["get"]!["security"]![0]!["Bearer"]);
            var ids = paths.SelectMany(path => path.Value!.AsObject().Where(method => method.Key is "get" or "post" or "put" or "delete" or "patch")
                .Select(method => method.Value?["operationId"]?.GetValue<string>())).Where(id => id is not null).ToArray();
            Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            Assert.True(!document.ToJsonString().Contains("passwordHash", StringComparison.OrdinalIgnoreCase));
            var descriptionGaps = OpenApiDescriptionCoverage.FindGaps(document, "/api/access");
            Assert.True(descriptionGaps.Count == 0, string.Join(Environment.NewLine, descriptionGaps));
            foreach (var path in new[] { "/api/processes/definitions", "/api/processes/definitions/{definitionKey}",
                "/api/processes/definitions/{definitionKey}/roles", "/api/processes/definitions/{definitionKey}/steps",
                "/api/workflows/templates", "/api/workflows/templates/{templateKey}/drafts", "/api/settings/workspace" }) {
                Assert.True(paths.ContainsKey(path), path);
            }
        }
    }

    [Fact]
    public async Task Active_stream_revalidates_disable_and_revocation_before_sending_a_new_event() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "stream-reader", password, [ApiAccessScopeNames.ReadWorkflows]);
        var session = await LoginAsync(host.Client, user.UserName, password);
        var events = host.App.Services.GetRequiredService<ProfileBoundedReplayEventStream<WorkflowApiRunEvent>>();
        long after = 0;
        await AssertStreamClosesAsync(session.Token, async () => {
            SetToken(host.Client, admin.Token);
            await UpdateAsync(host.Client, user, false, user.Scopes);
        });
        var machine = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() { Subject = "stream-machine", Scopes = [ApiAccessScopeNames.ReadWorkflows] });
        var registry = host.App.Services.GetRequiredService<IApiTokenRegistry>();
        var record = Assert.Single((await registry.SearchAsync(new("stream-machine"))).Items);
        await AssertStreamClosesAsync(machine.Token, () => registry.RevokeAsync(record.Id, DateTimeOffset.UtcNow));
        var narrowed = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "stream-narrowed", Scopes = [ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.ReadProjects]
        });
        var narrowedSummary = Assert.Single((await registry.SearchAsync(new("stream-narrowed"))).Items);
        var narrowedRecord = (await registry.FindAsync(narrowedSummary.Id))!;
        await AssertStreamClosesAsync(narrowed.Token, async () => {
            await registry.DeleteAsync(narrowedRecord.Id);
            registry.Register(narrowedRecord with { Scopes = [ApiAccessScopeNames.ReadProjects] });
        });

        async Task AssertStreamClosesAsync(string token, Func<Task> revoke) {
            SetToken(host.Client, token);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await host.Client.GetAsync($"/api/workflows/events/stream?after={after}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
            after = events.Publish(new(Guid.NewGuid(), WorkflowRunId.New(), WorkflowApiEventCategory.Progress,
                WorkflowEventKind.Output, null, DateTimeOffset.UtcNow, false, false));
            var received = false;
            while (await reader.ReadLineAsync(timeout.Token) is { } allowedLine) {
                if (allowedLine.StartsWith("data:", StringComparison.Ordinal)) {
                    received = true;
                    break;
                }
            }
            Assert.True(received, "The authorized stream closed before delivering its allowed event.");
            await revoke();
            after = events.Publish(new(Guid.NewGuid(), WorkflowRunId.New(), WorkflowApiEventCategory.Progress,
                WorkflowEventKind.Output, null, DateTimeOffset.UtcNow, false, false));
            try {
                while (await reader.ReadLineAsync(timeout.Token) is { } line) {
                    Assert.False(line.StartsWith("data:", StringComparison.Ordinal), "A forbidden event escaped after revocation.");
                }
            } catch (IOException) {
            }
            Assert.False(timeout.IsCancellationRequested, "The revoked stream did not close within 15 seconds.");
        }
    }

    [Theory]
    [InlineData(SessionInvalidation.PasswordReset)]
    [InlineData(SessionInvalidation.ScopeReduction)]
    [InlineData(SessionInvalidation.Expiry)]
    public async Task Idle_stream_closes_after_current_session_authority_changes(SessionInvalidation change) {
        var password = Password();
        var clock = new StreamClock { Now = DateTimeOffset.UtcNow };
        await using var host = await ApiTestHost.CreateAsync(true, services => services.AddSingleton<IClock>(clock),
            useInMemoryDatabase: true, apiConfiguration: new Dictionary<string, string?> {
                ["Api:UserAuthentication:Enabled"] = "true", ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
                ["Api:AccessManagement:Enabled"] = "true", ["Api:ServerSentEvents:HeartbeatIntervalSeconds"] = "1",
                ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
            });
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "idle-reader", password, [ApiAccessScopeNames.ReadWorkflows]);
        var session = await LoginAsync(host.Client, user.UserName, password);
        SetToken(host.Client, session.Token);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await host.Client.GetAsync("/api/workflows/events/stream", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        SetToken(host.Client, admin.Token);
        switch (change) {
            case SessionInvalidation.PasswordReset:
                using (var reset = await host.Client.PostAsJsonAsync($"/api/access/users/{user.Id}/reset-password",
                    new ApiUserPasswordResetRequest(Password(), user.Version))) {
                    Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
                }
                break;
            case SessionInvalidation.ScopeReduction:
                await UpdateAsync(host.Client, user, true, []);
                break;
            case SessionInvalidation.Expiry:
                clock.Now = session.ExpiresAtUtc;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        try {
            while (await reader.ReadLineAsync(timeout.Token) is { } line) {
                Assert.False(line.StartsWith("data:", StringComparison.Ordinal), "An idle stream delivered data after losing authority.");
            }
        } catch (IOException) {
        }
        Assert.False(timeout.IsCancellationRequested, "The idle stream did not close within the bounded revalidation interval.");
    }

    public enum SessionInvalidation { PasswordReset, ScopeReduction, Expiry }

    private sealed class StreamClock : IClock {
        public DateTimeOffset Now { get; set; }
        public DateTimeOffset GetUtcNow() => Now;
    }
}
