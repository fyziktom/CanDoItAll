using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class ProcessPlanningReviewApiTests {
    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.StepSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.StepSnapshot)]
    public async Task Real_HTTP_review_and_host_restart_recover_one_original_effect_and_accounting_receipt(
        AgentPlanningReviewChoice choice, ReadBasisMode mode) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile);
        var route = Route(fixture);
        string retainedRequest;
        Guid actionId;
        await using (var host = await CreateHostAsync(environment, profile, fixture.Clock)) {
            ProcessPlanningReviewHttpAccessTests.UseToken(host);
            var json = Options(host);
            using var read = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("no-store", read.Headers.CacheControl?.ToString());
            var text = await read.Content.ReadAsStringAsync();
            RequireSafe(text);
            var view = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(text, json)!;
            Assert.Equal(ProcessPlanningReviewState.AwaitingChoice, view.State);
            Assert.Contains(choice, view.AvailableChoices);
            Assert.Equal(AgentPlanningDecisionExpectation.From(fixture.Waiting), view.Expected);
            Assert.Equal(ProcessPlanningReviewApiExpectation.From(fixture.Request), view.Process);
            Assert.Equal(mode, view.ParentPolicy.ReadBasisMode);
            Assert.Equal(ReadBasisMode.RunSnapshot, view.ChildPolicy.ReadBasisMode);
            Assert.Equal(fixture.Waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentPolicy, view.ParentPolicy);
            Assert.Equal(fixture.Waiting.ToolAdmission.PlanningDecision.Policy, view.ChildPolicy);
            Assert.NotNull(view.Publication);
            actionId = Guid.NewGuid();
            ProcessPlanningReviewApiSubmission request = new(new(actionId, choice, view.Expected,
                view.Publication.Binding, view.Publication.ApprovalId), view.Process!);
            retainedRequest = JsonSerializer.Serialize(request, json);
            using var applied = await host.Client.PostAsync(route, Content(retainedRequest));
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
            Assert.Equal("no-store", applied.Headers.CacheControl?.ToString());
            var responseText = await applied.Content.ReadAsStringAsync();
            RequireSafe(responseText);
            var result = JsonSerializer.Deserialize<ProcessPlanningReviewApiResult>(responseText, json)!;
            Assert.Equal(actionId, result.ActionId);
            Assert.False(result.Replayed);
            Assert.False(result.AccountingPending);
            Assert.True(result.RequiresRuntimeReload);
            Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Completion);
            Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? RunOutcome.Succeeded : RunOutcome.Cancelled, result.Agent.Outcome);
            using var closed = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
            var settled = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(await closed.Content.ReadAsStringAsync(), json)!;
            Assert.Equal(ProcessPlanningReviewState.Settled, settled.State);
            Assert.Null(settled.Process);
            Assert.Null(settled.Publication);
            Assert.Empty(settled.AvailableChoices);
        }
        await using (var restarted = await CreateHostAsync(environment, profile, fixture.Clock)) {
            ProcessPlanningReviewHttpAccessTests.UseToken(restarted);
            using var recovered = await restarted.Client.PostAsync(route, Content(retainedRequest));
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            var text = await recovered.Content.ReadAsStringAsync();
            RequireSafe(text);
            var replay = JsonSerializer.Deserialize<ProcessPlanningReviewApiResult>(text, Options(restarted))!;
            Assert.Equal(actionId, replay.ActionId);
            Assert.True(replay.Replayed);
            Assert.False(replay.AccountingPending);
            Assert.True(replay.RequiresRuntimeReload);
            Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Completion);
            var changed = JsonNode.Parse(retainedRequest)!;
            changed["agent"]!["actionId"] = Guid.NewGuid();
            using var substituted = await restarted.Client.PostAsync(route, Content(changed.ToJsonString()));
            Assert.Equal(HttpStatusCode.Conflict, substituted.StatusCode);
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(choice == AgentPlanningReviewChoice.Approve ? 1 : 0,
            choice == AgentPlanningReviewChoice.Approve ? 1 : 0);
    }

    [Fact]
    public async Task Real_HTTP_rejects_wrong_identity_permission_and_unbounded_or_ambiguous_JSON_without_intent() {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(environment: environment, profile: profile);
        await using var host = await CreateHostAsync(environment, profile, fixture.Clock);
        var waiting = fixture.Waiting;
        ProcessPlanningReviewApiSubmission request = new(new(Guid.NewGuid(), AgentPlanningReviewChoice.Reject,
            AgentPlanningDecisionExpectation.From(waiting), ProjectTaskPlanAgentFixture.Binding(waiting),
            Assert.Single(waiting.PendingApprovals).ApprovalId), ProcessPlanningReviewApiExpectation.From(fixture.Request));
        var body = JsonSerializer.Serialize(request, Options(host));
        var before = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(waiting.Id));
        await RefusedAsync(Content(body), HttpStatusCode.Unauthorized);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ExecuteProcesses]);
        await RefusedAsync(Content(body), HttpStatusCode.Forbidden);
        ProcessPlanningReviewHttpAccessTests.UseToken(host);
        await RefusedAsync(new StringContent(body, Encoding.UTF8, "text/plain"), HttpStatusCode.UnsupportedMediaType);
        await RefusedAsync(Content(new string(' ', ProcessPlanningReviewApiSubmission.MaximumUtf8Bytes + 1)), HttpStatusCode.RequestEntityTooLarge);
        await RefusedAsync(new StreamingContent(new byte[ProcessPlanningReviewApiSubmission.MaximumUtf8Bytes + 1]), HttpStatusCode.RequestEntityTooLarge);
        foreach (var invalid in new[] { "null", "{}", body[..^1] + ",\"agent\":" + JsonSerializer.Serialize(request.Agent, Options(host)) + "}" }) {
            await RefusedAsync(Content(invalid), HttpStatusCode.BadRequest);
        }
        foreach (var revision in new[] { "1", "null", "\"01\"", "\"9223372036854775808\"" }) {
            var changed = JsonNode.Parse(body)!;
            changed["process"]!["decisionRevision"] = JsonNode.Parse(revision);
            await RefusedAsync(Content(changed.ToJsonString()), HttpStatusCode.BadRequest);
        }
        foreach (var member in new[] { "actor", "completionKey", "DecisionRevision" }) {
            var changed = JsonNode.Parse(body)!;
            changed["process"]![member] = "untrusted";
            await RefusedAsync(Content(changed.ToJsonString()), HttpStatusCode.BadRequest);
        }
        var changedRoute = JsonNode.Parse(body)!;
        changedRoute["process"]!["runId"] = Guid.NewGuid();
        await RefusedAsync(Content(changedRoute.ToJsonString()), HttpStatusCode.BadRequest);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(waiting.Id)));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);

        async Task RefusedAsync(HttpContent content, HttpStatusCode expected) {
            using (content) {
                using var response = await host.Client.PostAsync(Route(fixture), content);
                Assert.Equal(expected, response.StatusCode);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_accounting_or_lost_commit_acknowledgement_recovers_the_exact_HTTP_action(bool loseAcknowledgement) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(environment: environment, profile: profile);
        var waiting = fixture.Waiting;
        var fault = new CompletionFault(loseAcknowledgement);
        ProcessPlanningReviewApiSubmission request = new(new(Guid.NewGuid(), AgentPlanningReviewChoice.Approve,
            AgentPlanningDecisionExpectation.From(waiting), ProjectTaskPlanAgentFixture.Binding(waiting),
            Assert.Single(waiting.PendingApprovals).ApprovalId), ProcessPlanningReviewApiExpectation.From(fixture.Request));
        string retained;
        await using (var host = await CreateHostAsync(environment, profile, fixture.Clock, services =>
                services.AddScoped(provider => new ProcessDecisionReviewCoordinator(
                    provider.GetRequiredService<IProcessAcceptedExecutionInputReader>(),
                    provider.GetRequiredService<IProcessDecisionResultQuery>(),
                    provider.GetServices<IProcessDecisionChildOutcomeReader>(),
                    provider.GetRequiredService<IProcessIdempotencyStore>(),
                    new FaultedCompletion(provider.GetRequiredService<IProcessRuntimeUnitOfWork>(), fault),
                    NullLogger<ProcessDecisionReviewCoordinator>.Instance)))) {
            ProcessPlanningReviewHttpAccessTests.UseToken(host);
            retained = JsonSerializer.Serialize(request, Options(host));
            using var submitted = await host.Client.PostAsync(Route(fixture), Content(retained));
            Assert.Equal(loseAcknowledgement ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, submitted.StatusCode);
            var body = await submitted.Content.ReadAsStringAsync();
            if (loseAcknowledgement) {
                Assert.Contains(ProcessPlanningReviewApi.UnconfirmedCode, body, StringComparison.Ordinal);
            } else {
                var pending = JsonSerializer.Deserialize<ProcessPlanningReviewApiResult>(body, Options(host))!;
                Assert.True(pending.AccountingPending);
                Assert.Equal(RunOutcome.Succeeded, pending.Agent.Outcome);
                Assert.True(pending.RequiresRuntimeReload);
            }
            using var read = await host.Client.GetAsync(Route(fixture));
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var view = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(await read.Content.ReadAsStringAsync(), Options(host))!;
            Assert.Equal(loseAcknowledgement ? ProcessPlanningReviewState.Settled : ProcessPlanningReviewState.RecoveryRequired, view.State);
            Assert.Empty(view.AvailableChoices);
            Assert.Null(view.Process);
            Assert.Null(view.Publication);
        }
        await using (var restarted = await CreateHostAsync(environment, profile, fixture.Clock)) {
            ProcessPlanningReviewHttpAccessTests.UseToken(restarted);
            using var recovered = await restarted.Client.PostAsync(Route(fixture), Content(retained));
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            var result = JsonSerializer.Deserialize<ProcessPlanningReviewApiResult>(await recovered.Content.ReadAsStringAsync(), Options(restarted))!;
            Assert.True(result.Replayed);
            Assert.False(result.AccountingPending);
            Assert.Equal(request.Agent.ActionId, result.ActionId);
            Assert.Equal(loseAcknowledgement ? ProcessRuntimeTransitionOutcome.Duplicate : ProcessRuntimeTransitionOutcome.Applied, result.Completion);
        }
        Assert.Equal(1, fault.Attempts);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    private static Task<ApiTestHost> CreateHostAsync(CanDoItAllTestEnvironment environment, TestDatabaseProfile profile, TimeProvider clock,
        Action<IServiceCollection>? configure = null)
        => ApiTestHost.CreateAsync(true, services => {
                services.AddSingleton(clock);
                configure?.Invoke(services);
            },
            sharedTestEnvironment: environment, sharedActiveProfile: profile);

    private static JsonSerializerOptions Options(ApiTestHost host)
        => host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

    private static string Route(ProjectTaskPlanReviewFixture fixture)
        => $"/api/processes/runs/{fixture.State.RunId.Value:D}/steps/{fixture.Request.Child.StepInstanceId.Value:D}/planning/{fixture.Waiting.Id:D}/review";

    private static StringContent Content(string body) => new(body, Encoding.UTF8, "application/json");

    private static void RequireSafe(string text) {
        using var document = JsonDocument.Parse(text);
        Visit(document.RootElement);

        static void Visit(JsonElement element) {
            if (element.ValueKind == JsonValueKind.Object) {
                foreach (var property in element.EnumerateObject()) {
                    Assert.DoesNotContain(property.Name, new[] { "metadataJson", "toolAdmission", "backgroundSource",
                        "parentBinding", "originalTarget", "session", "ownerIntent", "claims", "currentState" });
                    if (property.Name.EndsWith("Revision", StringComparison.OrdinalIgnoreCase)) {
                        Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
                    }
                    Visit(property.Value);
                }
            } else if (element.ValueKind == JsonValueKind.Array) {
                foreach (var child in element.EnumerateArray()) {
                    Visit(child);
                }
            }
        }
    }

    private sealed class CompletionFault(bool loseAcknowledgement) {
        internal bool LoseAcknowledgement => loseAcknowledgement;
        internal int Attempts;
    }

    private sealed class FaultedCompletion(IProcessRuntimeUnitOfWork inner, CompletionFault fault) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            Assert.IsType<ProcessDecisionClosure>(request.Decision);
            Assert.IsType<ProcessDecisionBudgetSettlement>(request.DecisionBudget);
            Assert.Equal(1, Interlocked.Increment(ref fault.Attempts));
            if (!fault.LoseAcknowledgement) {
                return ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(request.OriginalState,
                    "Runtime.ReviewFixtureConflict", "A deterministic concurrent accounting refusal."));
            }
            var committed = await inner.CommitAsync(request, cancellationToken);
            Assert.True(committed.Succeeded);
            throw new IOException("The Process commit acknowledgement was lost after persistence.");
        }
    }

    private sealed class StreamingContent : HttpContent {
        private readonly byte[] bytes;

        internal StreamingContent(byte[] bytes) {
            this.bytes = bytes;
            Headers.ContentType = new("application/json");
        }

        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();
    }
}
