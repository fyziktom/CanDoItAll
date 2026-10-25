using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentEffectRecoveryApiTests {
    [Fact]
    public async Task Rest_recovery_returns_typed_waiting_without_metric_or_assistant() {
        await using var fixture = await HttpFixture.CreateAsync();
        using var response = await fixture.RecoverAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(fixture.Owner.Original.Id, body.GetProperty("executionRunId").GetGuid());
        Assert.Equal((int)ExecutionState.WaitingOnTool, body.GetProperty("state").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("metric").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assistantMessage").ValueKind);
        Assert.Equal(string.Empty, body.GetProperty("responseText").GetString());
        Assert.Equal(1, body.GetProperty("effectContinuation").GetProperty("counts").GetProperty("knownCommitted").GetInt32());
        AssertSanitized(body.GetRawText());
        Assert.Equal(0, fixture.Owner.Runtime.Calls);
    }

    [Fact]
    public async Task Run_detail_reconstructs_sanitized_continuation_without_journal_payload() {
        await using var fixture = await HttpFixture.CreateAsync();
        using var accepted = await fixture.RecoverAsync();
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        fixture.Owner.Receipts.Observe = (_, _) => throw new IOException("No historical receipt detail remains.");
        using var response = await fixture.Host.Client.GetAsync(fixture.RunRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var continuation = document.RootElement.GetProperty("effectContinuation");
        Assert.Equal((int)AgentEffectReconciliationStop.SnapshotRead, continuation.GetProperty("stop").GetInt32());
        Assert.Equal(1, continuation.GetProperty("counts").GetProperty("knownCommitted").GetInt32());
        Assert.Equal(0, continuation.GetProperty("examinedThisSlice").GetInt32());
        Assert.Equal(1, fixture.Owner.Receipts.Calls);
        AssertSanitized(document.RootElement.GetRawText());
        fixture.Owner.Resolver.ReadAllowed = false;
        using var denied = await fixture.Host.Client.GetAsync(fixture.RunRoute);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain("effectContinuation", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed,
            (await fixture.Owner.Store.GetExecutionRunAsync(fixture.Owner.Original.Id))!.ToolAdmission!.Batches[0].Proposals[0].State);
    }

    [Fact]
    public async Task Rest_recovery_and_existing_activity_stream_preserve_truthful_waiting() {
        await using var fixture = await HttpFixture.CreateAsync();
        var operation = AgentExecutionOperationId.New();
        using var accepted = await fixture.RecoverAsync(operation);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(operation.Value.ToString("D"), Assert.Single(accepted.Headers.GetValues(AgentApiHeaderNames.ActivityOperationId)));
        using var response = await fixture.Host.Client.GetAsync($"/api/agents/execution-operations/{operation.Value:D}/events/stream");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"phase\":\"awaitingContinuation\"", body, StringComparison.Ordinal);
        Assert.Contains("\"terminalOutcome\":\"suspended\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("agent.approval.waiting", body, StringComparison.Ordinal);
        Assert.DoesNotContain("agent.activity.completed", body, StringComparison.Ordinal);
        Assert.DoesNotContain("agent.command.completed", body, StringComparison.Ordinal);
        AssertSanitized(body);
        Assert.Equal(0, fixture.Owner.Runtime.Calls);
    }

    [Fact]
    public async Task Denied_or_unavailable_receipt_response_cannot_disclose_cached_bytes_or_offer_retry() {
        foreach (var failure in Enum.GetValues<Failure>()) {
            var armed = false;
            await using var fixture = await HttpFixture.CreateAsync(stage => {
                if (armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                    armed = false;
                    throw new IOException("Private checkpoint acknowledgement fault.");
                }
            }, failLeaseRelease: failure is Failure.LeaseRelease or Failure.CheckpointAndLease or Failure.DeniedAndLease or Failure.ReadUnavailableAndLease or Failure.CancelledAndLease);
            if (failure is Failure.Denied or Failure.DeniedAndLease) {
                fixture.Owner.Resolver.ReadAllowed = false;
            } else if (failure is Failure.ReadUnavailable or Failure.ReadUnavailableAndLease) {
                fixture.Owner.Resolver.Change = _ => throw new IOException("Private authority storage fault.");
            } else if (failure == Failure.CancelledAndLease) {
                fixture.Owner.Receipts.Observe = (_, _) => throw new OperationCanceledException("Private receipt cancellation.");
            } else {
                fixture.Owner.Receipts.Observe = async (_, token) => {
                    if (failure == Failure.CheckpointConflict) {
                        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Owner.Fixture.NewStore()).UpdateExecutionRunDetailAsync(
                            fixture.Owner.Original.Id, detail => detail with { Run = detail.Run with {
                                State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, Revision = checked(detail.Run.Revision + 1),
                                CompletedAtUtc = DateTimeOffset.UtcNow
                            } }, token);
                    } else if (failure is Failure.CheckpointAcknowledgementLost or Failure.CheckpointAndLease) {
                        armed = true;
                    }
                    return new(new("owner", "private-source"), AgentToolAdmissionJournalFixture.Envelope("{\"private\":true}"));
                };
            }
            using var response = await fixture.RecoverAsync();
            Assert.Equal(failure switch {
                Failure.Denied or Failure.DeniedAndLease => HttpStatusCode.Forbidden,
                Failure.CheckpointConflict => HttpStatusCode.Conflict,
                _ => HttpStatusCode.ServiceUnavailable
            }, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var body = document.RootElement;
            Assert.Equal(failure switch {
                Failure.Denied or Failure.DeniedAndLease => AgentEffectRecoveryApiResults.ReadDenied,
                Failure.ReadUnavailable or Failure.ReadUnavailableAndLease => AgentEffectRecoveryApiResults.ReadUnavailable,
                Failure.LeaseRelease or Failure.CancelledAndLease => AgentEffectRecoveryApiResults.LeaseReleaseUnconfirmed,
                _ => AgentEffectRecoveryApiResults.CheckpointIncomplete
            }, body.GetProperty("errors")[0].GetProperty("code").GetString());
            if (failure is Failure.CheckpointConflict or Failure.CheckpointAcknowledgementLost or Failure.CheckpointAndLease) {
                var continuation = body.GetProperty("effectContinuation");
                Assert.Equal((int)(failure == Failure.CheckpointConflict ? AgentEffectCheckpointPersistence.NotAttempted : AgentEffectCheckpointPersistence.Unconfirmed),
                    continuation.GetProperty("persistence").GetInt32());
                Assert.False(continuation.GetProperty("observations")[0].GetProperty("persisted").GetBoolean());
                Assert.Contains("not permission to retry", body.GetRawText(), StringComparison.Ordinal);
                if (failure == Failure.CheckpointAndLease) {
                    Assert.True(body.GetProperty("effectLeaseReleaseUnconfirmed").GetBoolean());
                    Assert.Equal(1, fixture.Owner.LeaseReleaseFaults);
                }
            } else if (failure == Failure.LeaseRelease) {
                Assert.Equal((int)AgentEffectCheckpointPersistence.Confirmed, body.GetProperty("effectContinuation").GetProperty("persistence").GetInt32());
                Assert.True(body.GetProperty("effectContinuation").GetProperty("observations")[0].GetProperty("persisted").GetBoolean());
                Assert.Equal(1, fixture.Owner.LeaseReleaseFaults);
                Assert.True(body.GetProperty("effectLeaseReleaseUnconfirmed").GetBoolean());
            } else {
                Assert.False(body.TryGetProperty("effectContinuation", out _));
                if (failure is Failure.DeniedAndLease or Failure.ReadUnavailableAndLease or Failure.CancelledAndLease) {
                    Assert.True(body.GetProperty("effectLeaseReleaseUnconfirmed").GetBoolean());
                    Assert.Equal(1, fixture.Owner.LeaseReleaseFaults);
                    Assert.Equal(AgentToolProposalState.Executing,
                        (await fixture.Owner.Store.GetExecutionRunAsync(fixture.Owner.Original.Id))!.ToolAdmission!.Batches[0].Proposals[0].State);
                }
            }
            AssertSanitized(body.GetRawText());
            Assert.DoesNotContain("Private ", body.GetRawText(), StringComparison.Ordinal);
            Assert.Equal(0, fixture.Owner.Runtime.Calls);
        }
        foreach (var outcome in Enum.GetValues<ActivityFailure>()) {
            var faults = new AgentEffectActivityFaults(AgentEffectActivityFault.Report | AgentEffectActivityFault.FailOrCancel | AgentEffectActivityFault.Dispose);
            var armedCheckpoint = false;
            await using var fixture = await HttpFixture.CreateAsync(stage => {
                if (armedCheckpoint && stage == ExistingRunDetailCommitStage.RunPersisted) {
                    armedCheckpoint = false;
                    throw new IOException("Private checkpoint acknowledgement fault.");
                }
            }, failLeaseRelease: outcome == ActivityFailure.IncompleteAndLease, activityDecorator: faults.Decorate);
            if (outcome is ActivityFailure.Denied or ActivityFailure.Unavailable) {
                fixture.Owner.Resolver.Change = authority => {
                    faults.Armed = true;
                    if (outcome == ActivityFailure.Unavailable) {
                        throw new IOException("Private current authority fault.");
                    }
                    return new(authority.AuthorityId, authority.AgentId, authority.DatabaseProfileId,
                        authority.DatabaseProfileGeneration, authority.WorkspaceScope, false, false,
                        authority.PolicyVersion, authority.PolicyFingerprint, authority.ResolvedAtUtc);
                };
            } else {
                fixture.Owner.Receipts.Observe = (_, _) => {
                    faults.Armed = true;
                    if (outcome == ActivityFailure.Cancelled) {
                        throw new OperationCanceledException("Private receipt cancellation.");
                    }
                    armedCheckpoint = outcome == ActivityFailure.IncompleteAndLease;
                    return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "private-source"),
                        AgentToolAdmissionJournalFixture.Envelope()));
                };
            }
            using var response = await fixture.RecoverAsync();
            Assert.Equal(outcome == ActivityFailure.Denied ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var body = document.RootElement;
            Assert.True(body.GetProperty("effectActivityFinalizationFailed").GetBoolean());
            Assert.Equal(outcome switch {
                ActivityFailure.Denied => AgentEffectRecoveryApiResults.ReadDenied,
                ActivityFailure.Unavailable => AgentEffectRecoveryApiResults.ReadUnavailable,
                _ => AgentEffectRecoveryApiResults.ActivityFinalizationFailed
            }, body.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Equal(outcome == ActivityFailure.IncompleteAndLease,
                body.TryGetProperty("effectLeaseReleaseUnconfirmed", out var release) && release.GetBoolean());
            if (outcome is ActivityFailure.Confirmed or ActivityFailure.IncompleteAndLease) {
                var continuation = body.GetProperty("effectContinuation");
                Assert.Equal((int)(outcome == ActivityFailure.Confirmed ? AgentEffectCheckpointPersistence.Confirmed : AgentEffectCheckpointPersistence.Unconfirmed),
                    continuation.GetProperty("persistence").GetInt32());
                Assert.Equal(outcome == ActivityFailure.Confirmed, continuation.GetProperty("observations")[0].GetProperty("persisted").GetBoolean());
                Assert.Equal(fixture.Owner.Original.Id, continuation.GetProperty("executionRunId").GetGuid());
            } else {
                Assert.False(body.TryGetProperty("effectContinuation", out _));
            }
            Assert.Equal(1, faults.DisposeAttempts);
            Assert.True(faults.ActualDisposed);
            AssertSanitized(body.GetRawText());
            Assert.DoesNotContain("Private ", body.GetRawText(), StringComparison.Ordinal);
            Assert.Equal(0, fixture.Owner.Runtime.Calls);
        }
    }

    [Fact]
    public async Task Disconnected_recovery_does_not_erase_confirmed_effect_or_publish_task_completion() {
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var armed = false;
        await using var fixture = await HttpFixture.CreateAsync(stage => {
            if (armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                persisted.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) {
                    throw new TimeoutException("The owned HTTP checkpoint acknowledgement barrier was not released.");
                }
            }
        });
        fixture.Owner.Receipts.Observe = (_, _) => {
            armed = true;
            return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "private-source"), AgentToolAdmissionJournalFixture.Envelope()));
        };
        var operation = AgentExecutionOperationId.New();
        using var cancel = new CancellationTokenSource();
        var pending = fixture.RecoverAsync(operation, cancel.Token);
        try {
            await persisted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        } finally {
            release.Set();
        }
        await fixture.Forwarder.RecoveryExited.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var saved = (await fixture.Owner.Store.GetExecutionRunDetailAsync(fixture.Owner.Original.Id))!;
        Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, saved.Run.ToolAdmission!.Batches[0].Proposals[0].State);
        Assert.Equal(ExecutionState.WaitingOnTool, saved.Run.State);
        Assert.Empty(saved.Metrics);
        Assert.Empty(saved.UsageObservations);
        Assert.Empty(saved.ChatSession!.Messages);
        Assert.Equal(0, fixture.Owner.Runtime.Calls);
        await using var reader = fixture.Owner.Coordinator.OpenReader(fixture.Owner.Identity.CreateStreamId(operation), StreamSequence.Beginning);
        var observed = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await reader.ReadAsync());
        Assert.DoesNotContain(observed.Items, item => item.Event.Phase == AgentExecutionActivityPhase.Completed);
        Assert.DoesNotContain(observed.Items, item => item.Event.TerminalOutcome == AgentExecutionActivityTerminalOutcome.Succeeded);
    }

    private static void AssertSanitized(string body) {
        foreach (var privateMember in new[] { "private-owner-result", "private-source", "payloadJson", "sourcePreparation", "toolAdmission", "serializedSessionStateJson" }) {
            Assert.DoesNotContain(privateMember, body, StringComparison.Ordinal);
        }
    }

    private enum Failure { Denied, ReadUnavailable, CheckpointConflict, CheckpointAcknowledgementLost, LeaseRelease, CheckpointAndLease, DeniedAndLease, ReadUnavailableAndLease, CancelledAndLease }
    private enum ActivityFailure { Confirmed, IncompleteAndLease, Denied, Unavailable, Cancelled }

    private sealed class HttpFixture(ApiTestHost host, AgentEffectRecoveryFixture owner, ForwardWorkspace forwarder) : IAsyncDisposable {
        internal ApiTestHost Host { get; } = host;
        internal AgentEffectRecoveryFixture Owner { get; } = owner;
        internal ForwardWorkspace Forwarder { get; } = forwarder;
        internal string RunRoute => $"/api/agents/execution-runs/{Owner.Original.Id:D}";
        internal Task<HttpResponseMessage> RecoverAsync(AgentExecutionOperationId? operation = null, CancellationToken cancellationToken = default)
            => Host.Client.PostAsJsonAsync(RunRoute + "/recover", new AgentExecutionRecoveryApiRequest(operation), cancellationToken);

        internal static async Task<HttpFixture> CreateAsync(Action<ExistingRunDetailCommitStage>? fault = null, bool failLeaseRelease = false,
            Func<IAgentExecutionActivityCoordinator, IAgentExecutionActivityCoordinator>? activityDecorator = null) {
            var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ForwardWorkspace>();
            var forwarder = (ForwardWorkspace)(object)proxy;
            var coordinator = new AgentExecutionActivityCoordinator(
                new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System),
                TimeProvider.System);
            var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true, configureServices: services => {
                services.Replace(ServiceDescriptor.Singleton(proxy));
                services.Replace(ServiceDescriptor.Singleton(coordinator));
                services.Replace(ServiceDescriptor.Singleton<IAgentExecutionActivityCoordinator>(coordinator));
            });
            try {
                using var scope = host.App.Services.CreateScope();
                var profile = scope.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile().Profile;
                var generation = scope.ServiceProvider.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration();
                var owner = await AgentEffectRecoveryFixture.CreateAsync(fault: fault,
                    profileBinding: new(profile.Id, profile.Runtime.Fingerprint, generation),
                    activityScope: WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")), coordinator: coordinator,
                    failLeaseRelease: failLeaseRelease, activityDecorator: activityDecorator);
                forwarder.Target = owner.Service;
                return new(host, owner, forwarder);
            } catch {
                await host.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            try {
                await Host.DisposeAsync();
            } finally {
                await Owner.DisposeAsync();
            }
        }
    }

    public class ForwardWorkspace : DispatchProxy {
        internal IAgentFrameworkWorkspaceService? Target { get; set; }
        internal TaskCompletionSource RecoveryExited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            var target = Target ?? throw new InvalidOperationException("The HTTP fixture must bind its actual workspace before dispatch.");
            try {
                var result = method!.Invoke(target, args);
                return method.Name == nameof(IAgentFrameworkWorkspaceService.RecoverExecutionRunAsync)
                    ? TrackAsync((Task<ExecutionRunResult>)result!) : result;
            } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
        private async Task<ExecutionRunResult> TrackAsync(Task<ExecutionRunResult> pending) {
            try {
                return await pending;
            } finally {
                RecoveryExited.TrySetResult();
            }
        }
    }
}
