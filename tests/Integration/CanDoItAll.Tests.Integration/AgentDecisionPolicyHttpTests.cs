using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentDecisionPolicyHttpTests {
    [Fact]
    public async Task Real_owner_accepts_once_and_preserves_policy_across_ordinary_agent_edits() {
        await using var host = await ApiTestHost.CreateAsync(false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var ordinary = await workspace.GetAgentEditorAsync(agent.Id);
        var original = (await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(agent.Id)))!;
        var submission = new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), original.Expectation, Policy());
        using var response = await host.Client.PutAsJsonAsync(Route(agent.Id), submission);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var accepted = (await response.Content.ReadFromJsonAsync<AgentDecisionPolicyApiResult>())!;
        Assert.Equal(AgentDecisionPolicyApiOutcome.Accepted, accepted.Outcome);
        Assert.Equal(submission.OperationId, accepted.OperationId);
        Assert.Equal(submission.Policy, accepted.Snapshot!.Policy);
        Assert.Equal(original.Expectation.ToDomain().CatalogRevision.Next(), accepted.Snapshot.Expectation.ToDomain().CatalogRevision);
        Assert.Equal(accepted.Snapshot, await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(agent.Id)));
        using var conflict = await host.Client.PutAsJsonAsync(Route(agent.Id), submission);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(AgentDecisionDataPolicyRejection.Conflict,
            (await conflict.Content.ReadFromJsonAsync<AgentDecisionPolicyApiResult>())!.Rejection);
        await Assert.ThrowsAsync<AgentCatalogConcurrencyException>(() => workspace.SaveAgentAsync(ordinary));
        ordinary = await workspace.GetAgentEditorAsync(agent.Id);
        ordinary.Summary = "Ordinary edit after policy publication";
        await workspace.SaveAgentAsync(ordinary);
        var current = (await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(agent.Id)))!;
        Assert.Equal(submission.Policy, current.Policy);
        Assert.NotEqual(accepted.Snapshot.Expectation, current.Expectation);
        using var inherited = await host.Client.PutAsJsonAsync(Route(agent.Id),
            new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), current.Expectation, new()));
        Assert.Equal(HttpStatusCode.OK, inherited.StatusCode);
        Assert.Equal(new DecisionDataPolicyOverride(), (await inherited.Content.ReadFromJsonAsync<AgentDecisionPolicyApiResult>())!.Snapshot!.Policy);
        Assert.Equal(submission.Policy, accepted.Snapshot.Policy);
        using var lookup = await host.Client.PostAsJsonAsync(Route(agent.Id) + "/lookup", submission);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        Assert.Equal("no-store", lookup.Headers.CacheControl?.ToString());
        var historical = (await lookup.Content.ReadFromJsonAsync<AgentDecisionPolicyApiLookupResult>())!;
        Assert.Equal(AgentDecisionPolicyApiLookupOutcome.HistoricalAccepted, historical.Outcome);
        Assert.Equal(accepted.Snapshot, historical.HistoricalSnapshot);
        Assert.NotNull(historical.AcceptedAtUtc);
        Assert.Equal(submission.OperationId, historical.OperationId);
        using var changed = await host.Client.PostAsJsonAsync(Route(agent.Id) + "/lookup", submission with { Policy = new() });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(AgentDecisionPolicyApiLookupOutcome.Conflict, (await changed.Content.ReadFromJsonAsync<AgentDecisionPolicyApiLookupResult>())!.Outcome);
        using var absent = await host.Client.PostAsJsonAsync(Route(agent.Id) + "/lookup", submission with { OperationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(AgentDecisionPolicyApiLookupOutcome.NotObserved, (await absent.Content.ReadFromJsonAsync<AgentDecisionPolicyApiLookupResult>())!.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Strict_body_refuses_malformed_and_streamed_oversized_input_without_dispatch(bool lookup) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        var probe = (PolicyWorkspace)(object)workspace;
        await using var host = await HostAsync(workspace);
        var submission = new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), probe.Snapshot.Expectation, Policy());
        var valid = JsonSerializer.Serialize(submission, JsonSerializerOptions.Web);
        Task<HttpResponseMessage> SendAsync(Guid agentId, HttpContent content) => lookup
            ? host.Client.PostAsync(Route(agentId) + "/lookup", content) : host.Client.PutAsync(Route(agentId), content);
        var bodies = new List<string> { "null", "{}", "[]" };
        foreach (var property in new[] { "operationId", "expectation", "policy" }) {
            var absent = JsonNode.Parse(valid)!.AsObject();
            absent.Remove(property);
            bodies.Add(absent.ToJsonString());
            var nullValue = JsonNode.Parse(valid)!.AsObject();
            nullValue[property] = null;
            bodies.Add(nullValue.ToJsonString());
        }
        foreach (var location in new[] { "", "expectation", "policy" }) {
            var json = JsonNode.Parse(valid)!.AsObject();
            (location.Length == 0 ? json : json[location]!.AsObject())["unknown"] = true;
            bodies.Add(json.ToJsonString());
        }
        foreach (var value in new[] { "0", "01", "+1", "-1", "9223372036854775808", "1.0", "1e0", "1 " }) {
            var json = JsonNode.Parse(valid)!.AsObject();
            json["expectation"]!["catalogRevision"] = value;
            bodies.Add(json.ToJsonString());
        }
        foreach (var property in new[] { "catalogRevision", "databaseProfileGeneration" }) {
            var json = JsonNode.Parse(valid)!.AsObject();
            json["expectation"]![property] = 1;
            bodies.Add(json.ToJsonString());
        }
        bodies.Add(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal));
        bodies.Add(valid.Replace("\"readBasisMode\":0", "\"readBasisMode\":999", StringComparison.Ordinal));
        bodies.Add(valid.Replace("\"maximumReplans\":0", "\"maximumReplans\":0,\"MaximumReplans\":1", StringComparison.Ordinal));
        bodies.Add(valid.Replace("\"catalogRevision\":", "\"catalogRevision\":\"1\",\"catalogRevision\":", StringComparison.Ordinal));
        bodies.Add(valid.Replace(submission.OperationId.ToString(), Guid.Empty.ToString(), StringComparison.Ordinal));
        foreach (var body in bodies) {
            using var content = Content(body);
            using var response = await SendAsync(probe.AgentId, content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        foreach (var content in new HttpContent[] { Content(new string(' ', 8193)), new StreamingContent(new byte[8193]) }) {
            using (content) {
                using var response = await SendAsync(probe.AgentId, content);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            }
        }
        using var wrongMedia = new StringContent(valid, Encoding.UTF8, "text/plain");
        using var unsupported = await SendAsync(probe.AgentId, wrongMedia);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        using var wrongRouteContent = Content(valid);
        using var wrongRoute = await SendAsync(Guid.NewGuid(), wrongRouteContent);
        Assert.Equal(HttpStatusCode.BadRequest, wrongRoute.StatusCode);
        Assert.Equal(0, probe.Writes);
        Assert.Equal(0, probe.Lookups);
    }

    [Fact]
    public async Task Actual_owner_rejects_a_different_profile_generation_or_scope() {
        await using var host = await ApiTestHost.CreateAsync(false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agentId = (await workspace.ListAgentsAsync(false)).First().Id;
        var original = (await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(agentId)))!;
        foreach (var expectation in new[] {
            original.Expectation with { DatabaseProfileId = Guid.NewGuid() },
            original.Expectation with { DatabaseProfileGeneration = (original.Expectation.ToDomain().Workspace.DatabaseProfileGeneration.Value + 1).ToString(CultureInfo.InvariantCulture) },
            original.Expectation with { ScopeKind = WorkspaceScopeKind.Project, ScopeKey = Guid.NewGuid().ToString("N") }
        }) {
            using var response = await host.Client.PutAsJsonAsync(Route(agentId), new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), expectation, Policy()));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(AgentDecisionDataPolicyRejection.ContextChanged,
                (await response.Content.ReadFromJsonAsync<AgentDecisionPolicyApiResult>())!.Rejection);
        }
        Assert.Equal(original, await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(agentId)));
    }

    [Fact]
    public async Task Agent_read_permission_does_not_grant_policy_write_permission() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        var probe = (PolicyWorkspace)(object)workspace;
        await using var host = await HostAsync(workspace, true);
        using var anonymous = await host.Client.GetAsync(Route(probe.AgentId));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var submission = new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), probe.Snapshot.Expectation, Policy());
        using var anonymousLookup = await host.Client.PostAsJsonAsync(Route(probe.AgentId) + "/lookup", submission);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLookup.StatusCode);
        UseToken(host, ApiAccessScopeNames.ReadAgents);
        Assert.Equal(probe.Snapshot, await host.Client.GetFromJsonAsync<AgentDecisionPolicyApiSnapshot>(Route(probe.AgentId)));
        using var lookup = await host.Client.PostAsJsonAsync(Route(probe.AgentId) + "/lookup", submission);
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
        Assert.Equal(1, probe.Lookups);
        using var denied = await host.Client.PutAsJsonAsync(Route(probe.AgentId), submission);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, probe.Writes);
        UseToken(host, ApiAccessScopeNames.WriteAgents);
        using var accepted = await host.Client.PutAsJsonAsync(Route(probe.AgentId), submission);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(1, probe.Writes);
    }

    [Fact]
    public async Task Lookup_failure_is_sanitized_and_mismatched_owner_receipt_is_never_returned_as_acceptance() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        var probe = (PolicyWorkspace)(object)workspace;
        await using var host = await HostAsync(workspace);
        var submission = new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), probe.Snapshot.Expectation, Policy());
        foreach (var lookup in new AgentDecisionDataPolicyLookup[] {
            new AgentDecisionDataPolicyLookup.Unavailable(AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.OwnerDispatch,
                new IOException(PolicyWorkspace.PrivateMarker))),
            new AgentDecisionDataPolicyLookup.Accepted(new(new(Guid.NewGuid(), probe.Snapshot.Expectation.ToDomain(), Policy()), DateTimeOffset.UtcNow))
        }) {
            probe.LookupResult = lookup;
            using var response = await host.Client.PostAsJsonAsync(Route(probe.AgentId) + "/lookup", submission);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(PolicyWorkspace.PrivateMarker, text, StringComparison.Ordinal);
            var result = JsonSerializer.Deserialize<AgentDecisionPolicyApiLookupResult>(text, JsonSerializerOptions.Web)!;
            Assert.Equal(AgentDecisionPolicyApiLookupOutcome.Unavailable, result.Outcome);
            Assert.Equal(submission.OperationId, result.OperationId);
            Assert.Null(result.HistoricalSnapshot);
        }
        Assert.Equal(0, probe.Writes);
    }

    [Fact]
    public void Int64_expectations_and_explicit_inheritance_round_trip_without_numeric_rounding() {
        foreach (var value in new[] { 9007199254740993L, long.MaxValue }) {
            var expectation = new AgentDecisionDataPolicyExpectation(new(Guid.NewGuid(), WorkspaceScopeDescriptor.Organization("test"), new(value)),
                Guid.NewGuid(), new(value));
            var wire = AgentDecisionPolicyApiExpectation.From(expectation);
            var json = JsonSerializer.Serialize(wire, JsonSerializerOptions.Web);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), document.RootElement.GetProperty("catalogRevision").GetString());
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), document.RootElement.GetProperty("databaseProfileGeneration").GetString());
            Assert.Equal(expectation, JsonSerializer.Deserialize<AgentDecisionPolicyApiExpectation>(json, JsonSerializerOptions.Web)!.ToDomain());
        }
        var snapshot = new AgentDecisionPolicyApiSnapshot(AgentDecisionPolicyApiExpectation.From(new(
            new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(0)), Guid.NewGuid(), new(1))), new());
        var text = JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web);
        using var policy = JsonDocument.Parse(text);
        Assert.Equal(7, policy.RootElement.GetProperty("policy").EnumerateObject().Count());
        Assert.Equal(1, policy.RootElement.GetProperty("policy").GetProperty("schemaVersion").GetInt32());
        Assert.Equal(snapshot, JsonSerializer.Deserialize<AgentDecisionPolicyApiSnapshot>(text, JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Known_acceptance_unknown_outcomes_and_failed_reads_remain_distinct_and_sanitized() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        var probe = (PolicyWorkspace)(object)workspace;
        await using var host = await HostAsync(workspace);
        var submission = new AgentDecisionPolicyApiSubmission(Guid.NewGuid(), probe.Snapshot.Expectation, Policy());
        foreach (var outcome in Enum.GetValues<Fault>()) {
            probe.Fault = outcome;
            using var response = await host.Client.PutAsJsonAsync(Route(probe.AgentId), submission);
            var expected = outcome == Fault.AcceptedCleanup ? HttpStatusCode.OK
                : outcome == Fault.Rejected ? HttpStatusCode.Conflict : HttpStatusCode.ServiceUnavailable;
            Assert.Equal(expected, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(PolicyWorkspace.PrivateMarker, text, StringComparison.Ordinal);
            var result = JsonSerializer.Deserialize<AgentDecisionPolicyApiResult>(text, JsonSerializerOptions.Web)!;
            Assert.Equal(submission.OperationId, result.OperationId);
            Assert.Equal(outcome == Fault.AcceptedCleanup ? AgentDecisionPolicyApiOutcome.Accepted
                : outcome == Fault.Rejected ? AgentDecisionPolicyApiOutcome.Rejected : AgentDecisionPolicyApiOutcome.Unconfirmed, result.Outcome);
            Assert.Equal(outcome == Fault.AcceptedCleanup, result.FollowupRequired);
            Assert.Equal(outcome == Fault.AcceptedCleanup, result.Snapshot is not null);
        }
        probe.FailRead = true;
        using var read = await host.Client.GetAsync(Route(probe.AgentId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        var error = await read.Content.ReadAsStringAsync();
        Assert.Contains(AgentDecisionPolicyApi.ReadUnavailableCode, error, StringComparison.Ordinal);
        Assert.DoesNotContain(PolicyWorkspace.PrivateMarker, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Openapi_describes_exact_revision_strings_and_the_strict_override_envelope() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        await using var host = await HostAsync(workspace);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var expectation = schemas.GetProperty(nameof(AgentDecisionPolicyApiExpectation)).GetProperty("properties");
        foreach (var name in new[] { "catalogRevision", "databaseProfileGeneration" }) {
            Assert.Equal("string", expectation.GetProperty(name).GetProperty("type").GetString());
            Assert.Equal(19, expectation.GetProperty(name).GetProperty("maxLength").GetInt32());
            Assert.Contains("JavaScript", expectation.GetProperty(name).GetProperty("description").GetString(), StringComparison.Ordinal);
        }
        var submission = schemas.GetProperty(nameof(AgentDecisionPolicyApiSubmission));
        var policy = submission.GetProperty("properties").GetProperty("policy");
        Assert.Equal(7, policy.GetProperty("required").GetArrayLength());
        Assert.False(policy.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(1, policy.GetProperty("properties").GetProperty("schemaVersion").GetProperty("enum")[0].GetInt32());
        Assert.Contains("null inherits", policy.GetProperty("properties").GetProperty("readBasisMode").GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.False(submission.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(3, submission.GetProperty("required").GetArrayLength());
        var lookup = document.RootElement.GetProperty("paths").GetProperty("/api/agents/{agentId}/decision-data-policy/lookup").GetProperty("post");
        Assert.Equal("LookupAgentDecisionDataPolicyOperation", lookup.GetProperty("operationId").GetString());
        Assert.Contains("30 days", lookup.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    private static DecisionDataPolicyOverride Policy() => new(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent,
        OnBasisChange.PauseForReview, 0, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5));
    private static string Route(Guid agentId) => $"/api/agents/{agentId:D}/decision-data-policy";
    private static StringContent Content(string text) => new(text, Encoding.UTF8, "application/json");
    private static Task<ApiTestHost> HostAsync(IAgentFrameworkWorkspaceService workspace, bool jwt = false)
        => ApiTestHost.CreateAsync(jwt, useInMemoryDatabase: true,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(workspace)));
    private static void UseToken(ApiTestHost host, params string[] scopes) {
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "agent-policy-http-test", Scopes = [.. scopes], LifetimeMinutes = 10
        });
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
    }

    public enum Fault { AcceptedCleanup, Rejected, Unknown, Throw }

    public class PolicyWorkspace : DispatchProxy {
        public const string PrivateMarker = "private-policy-diagnostic";
        public Guid AgentId { get; } = Guid.NewGuid();
        internal AgentDecisionPolicyApiSnapshot Snapshot => new(AgentDecisionPolicyApiExpectation.From(
            new(Workspace, AgentId, new(1))), null);
        private AgentExecutionActivityWorkspaceIdentity Workspace { get; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(0));
        public Fault Fault { get; set; }
        public bool FailRead { get; set; }
        public int Writes { get; private set; }
        public int Lookups { get; private set; }
        public AgentDecisionDataPolicyLookup LookupResult { get; set; } = new AgentDecisionDataPolicyLookup.NotObserved();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.LookupAgentDecisionDataPolicyAsync)) {
                Lookups++;
                return Task.FromResult(LookupResult);
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ReadAgentDecisionDataPolicyAsync)) {
                return FailRead ? Task.FromException<AgentDecisionDataPolicySnapshot>(new IOException(PrivateMarker))
                    : Task.FromResult(new AgentDecisionDataPolicySnapshot(Snapshot.Expectation.ToDomain(), null));
            }
            if (targetMethod?.Name != nameof(IAgentFrameworkWorkspaceService.ReplaceAgentDecisionDataPolicyAsync)) {
                throw new InvalidOperationException("Unexpected policy test dispatch.");
            }
            Writes++;
            var command = (ReplaceAgentDecisionDataPolicyCommand)args![0]!;
            var failure = AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.Publication, new IOException(PrivateMarker));
            AgentDecisionDataPolicySaveOutcome outcome = Fault switch {
                Fault.AcceptedCleanup => new AgentDecisionDataPolicySaveOutcome.Accepted(command,
                    new(new(command.Expectation.Workspace, AgentId, command.Expectation.CatalogRevision.Next()), command.Policy), true, failure),
                Fault.Rejected => new AgentDecisionDataPolicySaveOutcome.Rejected(AgentDecisionDataPolicyRejection.Conflict, failure),
                Fault.Unknown => new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command, failure),
                _ => throw new IOException(PrivateMarker)
            };
            return Task.FromResult(outcome);
        }
    }

    private sealed class StreamingContent : HttpContent {
        private readonly byte[] bytes;
        public StreamingContent(byte[] bytes) {
            this.bytes = bytes;
            Headers.ContentType = new("application/json");
        }
        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
