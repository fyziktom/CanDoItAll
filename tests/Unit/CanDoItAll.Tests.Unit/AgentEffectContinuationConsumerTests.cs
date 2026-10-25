using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentEffectContinuationConsumerTests {
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Chat_mapper_preserves_the_original_effect_checkpoint_without_fabricating_a_message_or_usage() {
        var continuation = Continuation(Guid.NewGuid());
        var result = ExecutionRunResult.ForEffectContinuation(continuation);

        var chat = AgentFrameworkWorkspaceExecutionService.ToAgentChatRunResult(result, continuation.ChatSessionId!.Value);

        Assert.Same(continuation, chat.EffectContinuation);
        Assert.Equal(continuation.ExecutionRunId, chat.ExecutionRunId);
        Assert.Equal(continuation.ChatSessionId, chat.ChatSessionId);
        Assert.Equal(ExecutionState.WaitingOnTool, chat.State);
        Assert.Null(chat.AssistantMessage);
        Assert.Null(chat.Metric);
        Assert.Null(chat.ContextCompletionNotification);
        Assert.Throws<InvalidOperationException>(() => AgentFrameworkWorkspaceExecutionService.ToAgentChatRunResult(result, Guid.NewGuid()));
    }

    [Fact]
    public void Process_recovery_retains_the_original_run_and_refuses_a_transient_replacement_dispatch() {
        var continuation = Continuation(null);
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(), ProcessInstancePlanId.New(), ProcessStepInstanceId.New(),
            "artifact", "worker", string.Empty, "Worker", "agent", Guid.NewGuid().ToString("D"), "Worker",
            "Create an artifact", "ready", "fixture", [], [], [], string.Empty,
            new Dictionary<string, string>(), null, Now);

        var issue = ProcessAgentExecutionRecoveryPolicy.EffectContinuationIssue(assignment, continuation);

        Assert.Equal(ProcessExecutionAdapterDiagnosticCodes.AgentEffectContinuationRequired, issue.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, issue.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.NonIdempotent, issue.Idempotency);
        Assert.Contains(continuation.ExecutionRunId.ToString("D"), issue.Evidence, StringComparison.Ordinal);
        Assert.Contains("Do not dispatch a replacement execution", issue.Summary, StringComparison.Ordinal);
        Assert.Null(issue.ExecutionSafetyAttestation);
        Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(
            assignment, ExecutionRunResult.ForEffectContinuation(continuation), out var retry));
        Assert.Null(retry);
        var cleanupFailure = new IOException("PRIVATE_LEASE_PATH");
        var cleanup = new AgentEffectReconciliationCleanupException(continuation, cleanupFailure);
        Assert.Same(continuation, cleanup.Continuation);
        Assert.Same(cleanupFailure, cleanup.InnerException);
        Assert.DoesNotContain("PRIVATE_LEASE_PATH", cleanup.Message, StringComparison.Ordinal);
        var cleanupIssue = ProcessAgentExecutionRecoveryPolicy.EffectContinuationIssue(assignment, cleanup.Continuation);
        Assert.Equal(issue, cleanupIssue);
        var unconfirmed = new AgentEffectContinuation(continuation.ExecutionRunId, null, 2, 2,
            AgentToolContinuationRequirement.EffectReconciliationRequired, new(1, 0, 1, 0, 0, 0, 0), 0, 1,
            AgentEffectReconciliationStop.CheckpointFailure, AgentEffectCheckpointPersistence.Unconfirmed, []);
        Assert.Throws<ArgumentException>(() => new AgentEffectReconciliationCleanupException(unconfirmed, cleanupFailure));
        var transientCleanup = new TimeoutException("PRIVATE_LEASE_TIMEOUT");
        var incomplete = new AgentEffectReconciliationIncompleteException(unconfirmed);
        var incompleteCleanup = incomplete.WithCleanupFailure(transientCleanup);
        Assert.Same(unconfirmed, incompleteCleanup.Continuation);
        Assert.True(incompleteCleanup.LeaseReleaseUnconfirmed);
        Assert.Equal(new Exception[] { incomplete, transientCleanup },
            Assert.IsType<AggregateException>(incompleteCleanup.InnerException).InnerExceptions);
        var confirmedCleanup = new AgentEffectReconciliationCleanupException(continuation, transientCleanup);
        foreach (var failure in new Exception[] { incompleteCleanup, confirmedCleanup }) {
            Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(assignment, failure, out retry));
            Assert.Null(retry);
            Assert.DoesNotContain("PRIVATE_LEASE_TIMEOUT", failure.Message, StringComparison.Ordinal);
        }
        var unconfirmedIssue = ProcessAgentExecutionRecoveryPolicy.EffectContinuationIssue(assignment, incompleteCleanup.Continuation);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, unconfirmedIssue.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.NonIdempotent, unconfirmedIssue.Idempotency);
        Assert.Contains(continuation.ExecutionRunId.ToString("D"), unconfirmedIssue.Evidence, StringComparison.Ordinal);
        var originalFailures = new (Exception Failure, AgentEffectReconciliationFailureKind Kind)[] {
            (new AgentToolReceiptAccessDeniedException(), AgentEffectReconciliationFailureKind.ReadDenied),
            (new AgentToolReceiptReadUnavailableException(new IOException("PRIVATE_READ_PATH")), AgentEffectReconciliationFailureKind.ReadUnavailable),
            (new OperationCanceledException(), AgentEffectReconciliationFailureKind.Cancelled),
            (new AgentToolAdmissionException("fixture-admission", "The original run cannot continue."), AgentEffectReconciliationFailureKind.AdmissionRejected)
        };
        foreach (var (originalFailure, kind) in originalFailures) {
            var leaseFailure = new AgentEffectReconciliationLeaseException(originalFailure, transientCleanup);
            Assert.Equal(kind, leaseFailure.FailureKind);
            Assert.Equal(new[] { originalFailure, transientCleanup },
                Assert.IsType<AggregateException>(leaseFailure.InnerException).InnerExceptions);
            Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(assignment, leaseFailure, out retry));
            Assert.Null(retry);
            var leaseIssue = ProcessAgentExecutionRecoveryPolicy.EffectLeaseReleaseIssue(assignment, leaseFailure,
                new ProcessExecutionRunId(continuation.ExecutionRunId));
            Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, leaseIssue.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.NonIdempotent, leaseIssue.Idempotency);
            Assert.Contains(continuation.ExecutionRunId.ToString("D"), leaseIssue.Evidence, StringComparison.Ordinal);
            Assert.Null(leaseIssue.ExecutionSafetyAttestation);
            Assert.DoesNotContain("PRIVATE_", leaseIssue.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_", leaseIssue.Evidence, StringComparison.Ordinal);
        }
        var stream = new AgentExecutionActivityStreamId(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox,
            new(0), AgentExecutionOperationId.New());
        var operation = Proxy<IAgentExecutionActivityOperationLease>((method, _) => {
            Assert.Equal("get_" + nameof(IAgentExecutionActivityOperationLease.StreamId), method.Name);
            return stream;
        });
        var publication = new TimeoutException("PRIVATE_ACTIVITY_TIMEOUT");
        var confirmedPublication = AgentExecutionActivityPublicationException.PreserveEffectOutcome(
            operation, null, publication, continuation);
        Assert.Same(continuation, confirmedPublication.EffectContinuation);
        Assert.Same(publication, confirmedPublication.InnerException);
        Assert.Null(confirmedPublication.EffectFailureKind);
        Assert.False(confirmedPublication.EffectLeaseReleaseUnconfirmed);
        Assert.False(confirmedPublication.ActivityDisposalFailed);
        AssertActivityCannotRetry(confirmedPublication);
        foreach (var (primary, expected, leaseUnconfirmed) in new (Exception, AgentEffectContinuation, bool)[] {
            (incomplete, unconfirmed, false),
            (incompleteCleanup, unconfirmed, true),
            (confirmedCleanup, continuation, true)
        }) {
            var activityFailure = AgentExecutionActivityPublicationException.PreserveEffectOutcome(operation, primary, publication);
            Assert.Same(expected, activityFailure.EffectContinuation);
            Assert.Equal(leaseUnconfirmed, activityFailure.EffectLeaseReleaseUnconfirmed);
            Assert.False(activityFailure.ActivityDisposalFailed);
            Assert.Equal(new[] { primary, publication }, Assert.IsType<AggregateException>(activityFailure.InnerException).InnerExceptions);
            AssertActivityCannotRetry(activityFailure);
            var disposal = new IOException("PRIVATE_ACTIVITY_DISPOSE");
            var combined = AgentExecutionActivityPublicationException.PreserveEffectOutcome(
                operation, activityFailure, disposal, continuation, disposalFailed: true);
            Assert.Same(expected, combined.EffectContinuation);
            Assert.Equal(activityFailure.EffectFailureKind, combined.EffectFailureKind);
            Assert.Equal(leaseUnconfirmed, combined.EffectLeaseReleaseUnconfirmed);
            Assert.True(combined.ActivityDisposalFailed);
            Assert.Equal(new Exception[] { activityFailure, disposal }, Assert.IsType<AggregateException>(combined.InnerException).InnerExceptions);
            AssertActivityCannotRetry(combined);
        }
        foreach (var (originalFailure, kind) in originalFailures) {
            foreach (var primary in new[] { originalFailure, new AgentEffectReconciliationLeaseException(originalFailure, transientCleanup) }) {
                var activityFailure = AgentExecutionActivityPublicationException.PreserveEffectOutcome(operation, primary, publication);
                Assert.Null(activityFailure.EffectContinuation);
                Assert.Equal(kind, activityFailure.EffectFailureKind);
                Assert.Equal(primary is AgentEffectReconciliationLeaseException, activityFailure.EffectLeaseReleaseUnconfirmed);
                Assert.False(activityFailure.ActivityDisposalFailed);
                AssertActivityCannotRetry(activityFailure);
            }
        }
        var ordinaryPublication = new AgentExecutionActivityPublicationException(stream, AgentExecutionActivityPhase.Failed, publication);
        Assert.False(ordinaryPublication.HasEffectOutcome);
        Assert.Null(ordinaryPublication.EffectContinuation);
        Assert.Null(ordinaryPublication.EffectFailureKind);
        Assert.False(ordinaryPublication.EffectLeaseReleaseUnconfirmed);
        Assert.False(ordinaryPublication.ActivityDisposalFailed);

        void AssertActivityCannotRetry(AgentExecutionActivityPublicationException activityFailure) {
            Assert.True(activityFailure.HasEffectOutcome);
            Assert.Equal(stream, activityFailure.StreamId);
            Assert.False(ProcessAgentExecutionRecoveryPolicy.TryBuildRetryableAgentTransientExecutionIssue(assignment, activityFailure, out var activityRetry));
            Assert.Null(activityRetry);
            var activityIssue = ProcessAgentExecutionRecoveryPolicy.EffectActivityFinalizationIssue(assignment, activityFailure,
                new ProcessExecutionRunId(continuation.ExecutionRunId));
            Assert.Equal(ProcessExecutionAdapterDiagnosticCodes.AgentEffectContinuationRequired, activityIssue.Code);
            Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, activityIssue.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.NonIdempotent, activityIssue.Idempotency);
            Assert.Contains(continuation.ExecutionRunId.ToString("D"), activityIssue.Evidence, StringComparison.Ordinal);
            Assert.Null(activityIssue.ExecutionSafetyAttestation);
            Assert.DoesNotContain("PRIVATE_", activityFailure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_", activityIssue.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_", activityIssue.Evidence, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Narrative_generation_defers_the_same_source_instead_of_parsing_saved_effects_as_provider_output() {
        var agent = Manager();
        var continuation = Continuation(null);
        var result = ExecutionRunResult.ForEffectContinuation(continuation);
        var calls = 0;
        var workspace = Proxy<IAgentFrameworkWorkspaceService>((method, args) => {
            if (method.Name == nameof(IAgentFrameworkWorkspaceService.ListExecutionRunsAsync)) {
                Assert.Equal(AgentFrameworkProcessRunNarrativeGenerator.SourceKind, Assert.IsType<ExecutionRunQuery>(args[0]).SourceKind);
                return Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([]);
            }
            Assert.Equal(nameof(IAgentFrameworkWorkspaceService.ExecuteSameSourceRunAsync), method.Name);
            calls++;
            var source = Assert.IsType<ExecutionRunSourceKey>(args[0]);
            Assert.Equal(agent.Id, Assert.IsType<ExecutionRunRequest>(args[1]).AgentId);
            return Task.FromResult(new ExecutionRunSourceExecutionResult(ExecutionRunSourceDisposition.Created,
                Run(continuation.ExecutionRunId, agent.Id, null, source.SourceKind, source.SourceId), result));
        });
        var references = Proxy<IAgentReferenceDataProvider>((method, _) => {
            Assert.Equal(nameof(IAgentReferenceDataProvider.GetAsync), method.Name);
            return Task.FromResult(new AgentReferenceDataSnapshot(AgentReferenceDataSections.Agents, [agent], [],
                new Dictionary<Guid, ProviderProfile>(), Now, TimeSpan.Zero));
        });
        var generator = new AgentFrameworkProcessRunNarrativeGenerator(references, workspace,
            new ProcessRunManagerAgentSelector(), TimeProvider.System);
        var record = CompletedProcessRecord();

        var deferred = await Assert.ThrowsAsync<ProcessRunNarrativeGenerationDeferredException>(() => generator.GenerateAsync(record));

        Assert.Equal(continuation.ExecutionRunId, deferred.ExecutionRunId);
        Assert.Equal(ExecutionState.WaitingOnTool, deferred.ExecutionState);
        Assert.Equal(AgentFrameworkProcessRunNarrativeGenerator.SourceKind, deferred.SourceKind);
        Assert.Equal($"{record.Summary.Identity.RunId}:{record.Summary.SourceGlobalSequence}", deferred.SourceId);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Recovery_orchestrator_suspends_original_activity_without_publishing_context_completion() {
        foreach (var point in Enum.GetValues<ConsumerActivityFault>()) {
            var continuation = Continuation(Guid.NewGuid());
            var result = ExecutionRunResult.ForEffectContinuation(continuation);
            var profile = new ResolvedDatabaseProfile(new DatabaseProfileRecord {
                Id = Guid.NewGuid(), DisplayName = "Effect fixture", ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            }, DatabaseProfileResolutionSource.ExplicitOverride, "fixture");
            var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
            var calls = 0;
            var notifications = 0;
            var disposals = 0;
            var armed = false;
            var failures = new List<Exception>();
            var workspace = Proxy<IAgentFrameworkWorkspaceActivityExecutionService>((method, args) => {
                Assert.Equal(nameof(IAgentFrameworkWorkspaceActivityExecutionService.RecoverExecutionRunWithinOperationAsync), method.Name);
                Assert.Equal(continuation.ExecutionRunId, Assert.IsType<Guid>(args[1]));
                calls++;
                var operation = Assert.IsAssignableFrom<IAgentExecutionActivityOperationLease>(args[0]);
                operation.BindExecutionRun(continuation.ExecutionRunId, continuation.ChatSessionId);
                armed = true;
                return Task.FromResult(result);
            });
            var coordinator = new AgentExecutionActivityCoordinator(
                new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(
                    PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
            var decorated = Proxy<IAgentExecutionActivityCoordinator>((method, args) => {
                Assert.Equal(nameof(IAgentExecutionActivityCoordinator.AdmitOperation), method.Name);
                var admitted = Assert.IsType<AgentExecutionActivityAdmitted>(method.Invoke(coordinator, args));
                var operation = Proxy<IAgentExecutionActivityOperationLease>((member, arguments) => {
                    if (member.Name == nameof(IDisposable.Dispose)) {
                        disposals++;
                        var disposed = member.Invoke(admitted.Operation, arguments);
                        if (armed && (point is ConsumerActivityFault.Disposal or ConsumerActivityFault.PublicationAndDisposal)) {
                            var failure = new IOException("Private native activity disposal failure.");
                            failures.Add(failure);
                            throw failure;
                        }
                        return disposed;
                    }
                    if (armed && (point == ConsumerActivityFault.CurrentState && member.Name == "get_" + nameof(IAgentExecutionActivityOperationLease.IsTerminal) ||
                            (point is ConsumerActivityFault.Publication or ConsumerActivityFault.PublicationAndDisposal) &&
                            member.Name == nameof(IAgentExecutionActivityOperationLease.SuspendForContinuation))) {
                        var failure = new TimeoutException("Private native activity finalization failure.");
                        failures.Add(failure);
                        throw failure;
                    }
                    return member.Invoke(admitted.Operation, arguments);
                });
                return admitted with { Operation = operation };
            });
            var orchestrator = new AgentChatExecutionOrchestrator(workspace,
                Proxy<IAgentTurnContextCaptureService>((method, _) => throw new InvalidOperationException($"Unexpected context capture: {method.Name}")),
                Proxy<IAgentChatExecutionNotificationHub>((method, _) => {
                    Assert.Equal(nameof(IAgentChatExecutionNotificationHub.PublishAsync), method.Name);
                    notifications++;
                    return Task.CompletedTask;
                }), decorated,
                Proxy<ICanDoItAllAgentWorkspaceFactory>((method, _) => {
                    Assert.Equal(nameof(ICanDoItAllAgentWorkspaceFactory.GetOrganizationScope), method.Name);
                    return scope;
                }),
                Proxy<IDatabaseProfileRuntimeAccessor>((method, _) => {
                    Assert.Equal(nameof(IDatabaseProfileRuntimeAccessor.ResolveCurrentProfile), method.Name);
                    return profile;
                }), new FixedAgentExecutionProfileGenerationSource(new(0)));
            var original = new AgentExecutionActivityStreamId(profile.Profile.Id, scope, new(0), AgentExecutionOperationId.New());

            var recovery = orchestrator.StartRunRecovery(Guid.NewGuid(), continuation.ChatSessionId!.Value, continuation.ExecutionRunId, original);
            if (point == ConsumerActivityFault.None) {
                Assert.Same(result, await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Empty(failures);
            } else {
                var failure = await Assert.ThrowsAsync<AgentExecutionActivityPublicationException>(() => recovery.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.True(failure.HasEffectOutcome);
                Assert.Same(continuation, failure.EffectContinuation);
                Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, failure.EffectContinuation!.Persistence);
                Assert.False(failure.EffectLeaseReleaseUnconfirmed);
                Assert.Equal(point is ConsumerActivityFault.Disposal or ConsumerActivityFault.PublicationAndDisposal, failure.ActivityDisposalFailed);
                Assert.Equal(point == ConsumerActivityFault.PublicationAndDisposal ? 2 : 1, failures.Count);
                Assert.All(failures, expected => Assert.Contains(Flatten(failure), actual => ReferenceEquals(expected, actual)));
            }
            Assert.NotEqual(original.OperationId, recovery.StreamId.OperationId);
            Assert.Equal(1, calls);
            Assert.Equal(1, disposals);
            Assert.Equal(0, notifications);
            await using var reader = coordinator.OpenReader(recovery.StreamId, StreamSequence.Beginning);
            var events = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await reader.ReadAsync());
            var terminal = Assert.Single(events.Items, item => item.Event.IsTerminal).Event;
            Assert.Equal(continuation.ExecutionRunId, terminal.ExecutionRunId);
            Assert.DoesNotContain(events.Items, item => item.Event.Phase == AgentExecutionActivityPhase.Completed);
            if (point is ConsumerActivityFault.None or ConsumerActivityFault.Disposal) {
                Assert.Equal(AgentExecutionActivityPhase.AwaitingContinuation, terminal.Phase);
                Assert.Equal(AgentExecutionActivityTerminalOutcome.Suspended, terminal.TerminalOutcome);
            }
        }
        var unused = Proxy<IAgentExecutionActivityOperationLease>((method, _) =>
            throw new InvalidOperationException($"An untyped finalization must not touch the activity: {method.Name}"));
        Assert.Null(AgentEffectActivityFinalization.Continuation<object?>(null));
        Assert.Null(AgentEffectActivityFinalization.Continuation(new object()));
        var ordinaryResult = new ExecutionRunResult(Guid.NewGuid(), null, "Ordinary provider output", null,
            new AgentRunMetric(Guid.NewGuid(), Guid.NewGuid(), null, Now, RunOutcome.Succeeded, "fixture", "fixture", 0, 0, 0, 0));
        Assert.Null(AgentEffectActivityFinalization.Continuation(ordinaryResult));
        foreach (var primary in new Exception?[] { null, new AgentToolAdmissionException("ordinary-admission", "Ordinary admission refused.") }) {
            Assert.False(AgentEffectActivityFinalization.IsEffectFailure(primary));
            Assert.Throws<ArgumentException>(() => AgentEffectActivityFinalization.Publish(unused, null, primary));
            Assert.Throws<ArgumentException>(() => AgentEffectActivityFinalization.Dispose(unused, null, primary));
        }
        var captured = Continuation(Guid.NewGuid());
        var snapshot = new AgentEffectContinuation(captured.ExecutionRunId, captured.ChatSessionId, captured.RunRevision,
            captured.JournalRevision, captured.Requirement, captured.Counts, 0, 0,
            AgentEffectReconciliationStop.SnapshotRead, AgentEffectCheckpointPersistence.Confirmed, []);
        Assert.Throws<ArgumentException>(() => AgentEffectActivityFinalization.Publish(unused, snapshot, null));
        Assert.Throws<ArgumentException>(() => AgentEffectActivityFinalization.Dispose(unused, snapshot, null));
        var snapshotCoordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        var snapshotStream = new AgentExecutionActivityStreamId(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox,
            new(0), AgentExecutionOperationId.New());
        var snapshotOperation = Assert.IsType<AgentExecutionActivityAdmitted>(snapshotCoordinator.AdmitOperation(
            snapshotStream, Guid.NewGuid(), snapshot.ChatSessionId, "Read-only observation fixture.")).Operation;
        try {
            snapshotOperation.BindExecutionRun(snapshot.ExecutionRunId, snapshot.ChatSessionId);
            await using var beforeReader = snapshotCoordinator.OpenReader(snapshotStream, StreamSequence.Beginning);
            var before = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await beforeReader.ReadAsync());
            Assert.Throws<ArgumentException>(() => snapshotOperation.SuspendForContinuation(snapshot, "Cannot publish a GET observation."));
            Assert.False(snapshotOperation.IsTerminal);
            await using var afterReader = snapshotCoordinator.OpenReader(snapshotStream, StreamSequence.Beginning);
            var after = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await afterReader.ReadAsync());
            Assert.Equal(before.Items.ToArray(), after.Items.ToArray());
            Assert.DoesNotContain(after.Items, item => item.Event.Phase == AgentExecutionActivityPhase.AwaitingContinuation);
        } finally {
            snapshotOperation.Dispose();
        }

        static IEnumerable<Exception> Flatten(Exception failure) {
            yield return failure;
            IEnumerable<Exception> nested = failure is AggregateException aggregate ? aggregate.InnerExceptions :
                failure.InnerException is { } inner ? [inner] : [];
            foreach (var item in nested) {
                foreach (var child in Flatten(item)) {
                    yield return child;
                }
            }
        }
    }
    [Fact]
    public async Task Prepared_only_readback_keeps_the_original_intent_and_does_not_request_provider_input_or_dispatch() {
        var store = Proxy<IUnusedStore>((method, _) => throw new InvalidOperationException($"Readback must not access storage: {method.Name}"));
        var profile = new AgentToolProfileBinding(Guid.NewGuid(), "fixture-profile", new(1));
        var journal = new AgentToolAdmissionJournal(store, profile);
        var agentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var chat = new ChatSessionRecord(Guid.NewGuid(), agentId, "Original effects", Now, Now, [], LatestExecutionRunId: runId);
        var authority = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), agentId, profile.ProfileId,
            profile.Generation, WorkspaceScopeDescriptor.Sandbox, true, true, "fixture", "fixture-policy", Now);
        var context = new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()), new("agents"), new("agents"),
            "agents", "chat", 1, "fixture-context", Now);
        var run = Run(runId, agentId, chat.Id, "chat-session", chat.Id.ToString("N")) with {
            State = ExecutionState.Preparing, MetadataJson = AgentTurnContextMetadata.Apply("{}", context, authority)
        };
        var original = journal.CreateForNewRun(run, chat);
        var envelope = AgentToolProtocolEnvelope.Create("fixture-protocol", 1, "{}");
        var payload = new AgentToolPreparedPayload("fixture-write", 2, envelope.Digest, "{}",
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, envelope);
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, false,
            AgentToolProposalState.Prepared, ExecutionApprovalStatus.Approved, payload.Digest);
        var saved = original with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            Segments = [new(Guid.NewGuid(), 0, 0, null, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: Guid.NewGuid())],
            Batches = [new(new(Guid.NewGuid()), 0, payload.Digest, envelope, [proposal])]
        };
        saved.Validate();
        var detail = new ExecutionRunDetail(run with { State = ExecutionState.WaitingOnTool, ToolAdmission = saved }, chat, [], []);
        var currentReads = 0;
        ValueTask<bool> CurrentRead(AgentToolSessionObservation observation, CancellationToken _) {
            Assert.Equal(saved.Session, observation.Session);
            Assert.Equal(authority.AuthorityId, observation.Governance!.AuthorityId);
            currentReads++;
            return new(true);
        }

        var continuation = await journal.ReadInterruptedContinuationAsync(detail, CurrentRead, default);

        Assert.Equal(AgentToolContinuationRequirement.None, continuation.Requirement);
        Assert.True(continuation.HasPreparedEffects);
        Assert.Equal(1, continuation.Counts.PreparedEffects);
        Assert.Equal(0, continuation.Counts.EligibleOwnerEffects);
        Assert.Equal(0, continuation.Counts.UncertainProviderDispatches);
        Assert.Equal(0, continuation.ExaminedThisSlice);
        Assert.Empty(continuation.Observations);
        Assert.Equal(2, currentReads);
        Assert.Same(proposal, detail.Run.ToolAdmission!.Batches[0].Proposals[0]);
        Assert.Null(proposal.DispatchClaimId);
        Assert.Null(proposal.Result);
        var missingInput = detail with { Run = detail.Run with { ToolAdmission = saved with { Batches = [] } } };
        var inputRequired = await journal.ReadInterruptedContinuationAsync(missingInput, CurrentRead, default);
        Assert.Equal(AgentToolContinuationRequirement.ProviderInputRequired, inputRequired.Requirement);
        Assert.False(inputRequired.HasPreparedEffects);
    }

    private static AgentEffectContinuation Continuation(Guid? chatSessionId) => new(Guid.NewGuid(), chatSessionId, 2, 2,
        AgentToolContinuationRequirement.None, new(1, 1, 0, 0, 0, 0, 0), 0, 0,
        AgentEffectReconciliationStop.ExhaustedSnapshot, AgentEffectCheckpointPersistence.Confirmed, []);

    private static ExecutionRunRecord Run(Guid runId, Guid agentId, Guid? chatId, string sourceKind, string sourceId) => new(
        runId, agentId, chatId, "Original effects", sourceKind, sourceId, string.Empty, string.Empty, "fixture", "user", "{}",
        "Original input", string.Empty, string.Empty, string.Empty, ExecutionState.WaitingOnTool, null, Now, Now, Now, null,
        string.Empty, null, [], Revision: 1);

    private static AgentDefinition Manager() => new(Guid.NewGuid(), "Manager", "Manager", "Summary", "Instructions",
        AgentLifecycleStatus.Active, Guid.NewGuid(), "test-model", AgentWorkloadKind.Management, AgentChatHistoryMode.FrameworkManaged,
        0, false, false, "{}", false, string.Empty, AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true },
        [], [ProcessRunManagerAgentSelector.ProcessManagerTag], Now, Now);

    private static ProcessRunRecord CompletedProcessRecord() {
        var runId = ProcessRunId.New();
        var identity = new ProcessRunRecordIdentity(runId, runId, null, ProcessInstancePlanId.New(), ProcessDefinitionId.New(),
            ProcessDefinitionVersionId.New(), Guid.NewGuid());
        var metrics = new ProcessRunRecordMetrics(StartedAtUtc: Now, EndedAtUtc: Now, DurationMilliseconds: 0,
            TotalStepCount: 0, ExecutableStepCount: 0, CompletedStepCount: 0, FailedStepCount: 0,
            CancelledStepCount: 0, RepetitionCount: 0, ExecutionCount: 0, ReworkCount: 0, IncidentCount: 0,
            EscalationCount: 0, InputTokenCount: 0, CachedInputTokenCount: 0, OutputTokenCount: 0,
            ReasoningTokenCount: 0, TotalTokenCount: 0, EstimatedCost: 0, ActualCost: 0,
            ToolCallCount: 0, ArtifactCount: 0, SubprocessCount: 0);
        var summary = new ProcessRunRecordSummary(identity, ProcessRunDisposition.Succeeded, ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete, ProcessRunEvidenceSource.All, ProcessRunEvidenceSource.None, [],
            ProcessRunFactsStatus.Completed, 1, null, null, null, ProcessRunNarrativeStatus.Pending, 0, null, null, null,
            metrics, [], null, 42, 7, ProcessRunRecordSchema.CurrentVersion, Now);
        return new(summary, new ProcessRunHardFacts([], [], [], [], [], []));
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> invoke) where T : class {
        var proxy = DispatchProxy.Create<T, ConsumerProxy>();
        ((ConsumerProxy)(object)proxy).Handler = invoke;
        return proxy;
    }

    private enum ConsumerActivityFault { None, CurrentState, Publication, Disposal, PublicationAndDisposal }

    public interface IUnusedStore : ISandboxWorkspaceStore, ISandboxWorkspaceExecutionRunStore,
        ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
    }

    public class ConsumerProxy : DispatchProxy {
        public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return Handler(targetMethod, args ?? []);
        }
    }
}
