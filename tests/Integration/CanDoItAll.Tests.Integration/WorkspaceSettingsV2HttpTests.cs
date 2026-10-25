using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceSettingsV2HttpTests {
    private const string Route = "/api/settings/workspace/v2";

    [Fact]
    public async Task V2_exact_acceptance_conflicts_with_an_old_head_and_v1_preserves_the_configured_policy() {
        await using var host = await ApiTestHost.CreateAsync(false);
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route))!;
        Assert.Null(original.DecisionDataPolicy);
        var command = new ReplaceWorkspaceSettingsV2Command(original.Expectation, Values(" Policy accepted "), Policy());
        using var response = await host.Client.PutAsJsonAsync(Route, command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await response.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcomeV2>());
        Assert.Equal(command.DecisionDataPolicy, accepted.Snapshot.DecisionDataPolicy);
        Assert.Equal("Policy accepted", accepted.Snapshot.Values.WorkspaceName);
        Assert.NotEqual(original.Expectation, accepted.Snapshot.Expectation);
        Assert.Equal(accepted.Snapshot, await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route));
        using var conflict = await host.Client.PutAsJsonAsync(Route, command);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await conflict.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcomeV2>()).Reason);
        using var legacyResponse = await host.Client.PutAsJsonAsync("/api/settings/workspace",
            new ReplaceWorkspaceSettingsCommand(accepted.Snapshot.Expectation, Values("Legacy defaults edit")));
        Assert.Equal(HttpStatusCode.OK, legacyResponse.StatusCode);
        var legacy = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await legacyResponse.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>());
        var current = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route))!;
        Assert.Equal(legacy.Snapshot.Expectation, current.Expectation);
        Assert.Equal(legacy.Snapshot.Values, current.Values);
        Assert.Equal(accepted.Snapshot.DecisionDataPolicy, current.DecisionDataPolicy);
        Assert.NotEqual(accepted.Snapshot.Expectation, current.Expectation);
        Assert.Equal("Policy accepted", accepted.Snapshot.Values.WorkspaceName);
    }

    [Fact]
    public async Task Strict_v2_body_and_provider_validation_refuse_before_any_head_change() {
        await using var host = await ApiTestHost.CreateAsync(false);
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route))!;
        var command = new ReplaceWorkspaceSettingsV2Command(original.Expectation, Values("Invalid request"), Policy());
        var valid = JsonSerializer.Serialize(command, JsonSerializerOptions.Web);
        var bodies = new List<string> { "null", "{}", "[]", valid.Replace("\"schemaVersion\":2", "\"schemaVersion\":3", StringComparison.Ordinal) };
        foreach (var property in new[] { "expected", "values", "decisionDataPolicy" }) {
            var absent = JsonNode.Parse(valid)!.AsObject();
            absent.Remove(property);
            bodies.Add(absent.ToJsonString());
            var explicitNull = JsonNode.Parse(valid)!.AsObject();
            explicitNull[property] = null;
            bodies.Add(explicitNull.ToJsonString());
        }
        foreach (var location in new[] { "", "expected", "values", "decisionDataPolicy" }) {
            var json = JsonNode.Parse(valid)!.AsObject();
            var target = location.Length == 0 ? json : json[location]!.AsObject();
            target["unexpected"] = true;
            bodies.Add(json.ToJsonString());
        }
        bodies.Add(valid.Replace("\"maximumReplans\":0", "\"maximumReplans\":0,\"maximumReplans\":0", StringComparison.Ordinal));
        bodies.Add(valid.Replace("\"onBasisChange\":1", "\"onBasisChange\":99", StringComparison.Ordinal));
        bodies.Add(valid.Replace("\"maximumReplans\":0", "\"maximumReplans\":1", StringComparison.Ordinal));
        foreach (var body in bodies) {
            using var response = await host.Client.PutAsync(Route, new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(WorkspaceSettingsRejectionReason.InvalidValues,
                Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await response.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcomeV2>()).Reason);
        }
        using var oversized = await host.Client.PutAsync(Route, new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var wrongMedia = await host.Client.PutAsync(Route, new StringContent(valid, Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongMedia.StatusCode);
        var missingProvider = new WorkspaceSettingsValues("Unavailable provider", Guid.NewGuid(), "Markdown", "USD", "en-US", "");
        using var provider = await host.Client.PutAsJsonAsync(Route, new ReplaceWorkspaceSettingsV2Command(original.Expectation, missingProvider, Policy()));
        Assert.Equal(HttpStatusCode.BadRequest, provider.StatusCode);
        Assert.Equal(original, await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route));
    }

    [Fact]
    public async Task V2_reuses_workspace_read_and_write_capabilities_without_granting_a_reader_mutation_access() {
        var password = Password();
        await using var host = await CreateHostAsync(password, useInMemoryDatabase: false);
        using var unauthenticated = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var reader = await CreateUserAsync(host.Client, "policy-reader", password, [ApiAccessScopeNames.ReadWorkspaceSettings]);
        var session = await LoginAsync(host.Client, reader.UserName, password);
        SetToken(host.Client, session.Token);
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route))!;
        Assert.Equal(original, await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route));
        using var denied = await host.Client.PutAsJsonAsync(Route, new ReplaceWorkspaceSettingsV2Command(original.Expectation, Values("Denied"), Policy()));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        SetToken(host.Client, admin.Token);
        await UpdateAsync(host.Client, reader, true, [ApiAccessScopeNames.ReadWorkspaceSettings, ApiAccessScopeNames.WriteWorkspaceSettings]);
        session = await LoginAsync(host.Client, reader.UserName, password);
        SetToken(host.Client, session.Token);
        using var saved = await host.Client.PutAsJsonAsync(Route, new ReplaceWorkspaceSettingsV2Command(original.Expectation, Values("Authorized"), Policy()));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Authorized", (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshotV2>(Route))!.Values.WorkspaceName);
    }

    [Fact]
    public async Task Corrupt_stored_policy_returns_safe_503_and_never_appears_as_legacy_absence() {
        await using var host = await ApiTestHost.CreateAsync(false);
        await using var context = await host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        const string corrupt = "not-json-private-fixture-marker";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Workspace_Settings" SET "DecisionDataPolicyJson" = {corrupt} WHERE "IsCurrent"
            """);
        using var unavailable = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var failure = await unavailable.Content.ReadAsStringAsync();
        Assert.Contains("settings.workspace-policy-unavailable", failure, StringComparison.Ordinal);
        Assert.DoesNotContain(corrupt, failure, StringComparison.Ordinal);
        var legacy = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        using var saved = await host.Client.PutAsJsonAsync("/api/settings/workspace", new ReplaceWorkspaceSettingsCommand(legacy.Expectation, Values("Preserved invalid policy")));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(corrupt, await context.Set<WorkspaceSettings>().AsNoTracking().Where(row => row.IsCurrent).Select(row => row.DecisionDataPolicyJson).SingleAsync());
        using var stillUnavailable = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, stillUnavailable.StatusCode);
    }

    [Fact]
    public async Task Openapi_exposes_complete_v2_policy_and_keeps_the_v1_request_shape_unchanged() {
        await using var host = await ApiTestHost.CreateAsync(false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var write = paths.GetProperty(Route).GetProperty("put");
        var command = Resolve(write.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal(new[] { "decisionDataPolicy", "expected", "schemaVersion", "values" }, Names(command.GetProperty("properties")));
        Assert.Equal(4, command.GetProperty("required").GetArrayLength());
        Assert.False(command.GetProperty("additionalProperties").GetBoolean());
        var policy = Resolve(command.GetProperty("properties").GetProperty("decisionDataPolicy"));
        Assert.Equal(7, policy.GetProperty("required").GetArrayLength());
        Assert.False(policy.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("object", policy.GetProperty("type").GetString());
        foreach (var status in new[] { "200", "400", "409", "503", "401", "403", "413", "415" }) {
            Assert.True(write.GetProperty("responses").TryGetProperty(status, out _));
        }
        var read = paths.GetProperty(Route).GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var nullable = Resolve(read).GetProperty("properties").GetProperty("decisionDataPolicy").GetProperty("anyOf");
        Assert.Contains(nullable.EnumerateArray(), item => item.TryGetProperty("type", out var type) && type.GetString() == "null");
        var v1 = Resolve(paths.GetProperty("/api/settings/workspace").GetProperty("put").GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal(new[] { "expected", "values" }, Names(v1.GetProperty("properties")));
        var acceptedV2 = Response(write, "200");
        var acceptedV1 = Response(paths.GetProperty("/api/settings/workspace").GetProperty("put"), "200");
        Assert.NotEqual(acceptedV1.GetRawText(), acceptedV2.GetRawText());
        var committed = Resolve(Resolve(acceptedV2).GetProperty("properties").GetProperty("snapshot"));
        Assert.Equal(new[] { "decisionDataPolicy", "expectation", "schemaVersion", "values" }, Names(committed.GetProperty("properties")));
        Assert.Equal(4, committed.GetProperty("required").GetArrayLength());
        Assert.Equal(new[] { "expectation", "values" }, Names(Resolve(Resolve(acceptedV1).GetProperty("properties").GetProperty("snapshot")).GetProperty("properties")));
        var committedPolicy = committed.GetProperty("properties").GetProperty("decisionDataPolicy").GetProperty("anyOf");
        Assert.Equal(Names(policy.GetProperty("properties")), Names(Resolve(committedPolicy[0]).GetProperty("properties")));
        Assert.Equal("rejected", Resolve(Response(write, "409")).GetProperty("properties").GetProperty("status").GetProperty("enum")[0].GetString());
        Assert.Equal("unconfirmed", Resolve(Response(write, "503")).GetProperty("properties").GetProperty("status").GetProperty("enum")[0].GetString());

        static JsonElement Response(JsonElement operation, string status) => operation.GetProperty("responses").GetProperty(status)
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");

        JsonElement Resolve(JsonElement value) {
            while (true) {
                if (value.TryGetProperty("$ref", out var reference)) {
                    value = root;
                    foreach (var segment in reference.GetString()![2..].Split('/')) {
                        value = value.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
                    }
                } else if (value.TryGetProperty("allOf", out var allOf)) {
                    value = Assert.Single(allOf.EnumerateArray());
                } else {
                    return value;
                }
            }
        }

        static string[] Names(JsonElement value) => value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
    }

    private static WorkspaceSettingsValues Values(string name) => new(name, null, "Markdown", "USD", "en-US", "HTTP policy fixture");
    private static DecisionDataPolicy Policy() => new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent,
        OnBasisChange.PauseForReview, new(0, TimeSpan.FromMinutes(5) + TimeSpan.FromTicks(1), TimeSpan.FromMinutes(1)));
}
