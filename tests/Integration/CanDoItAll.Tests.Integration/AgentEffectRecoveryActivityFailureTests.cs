using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentEffectRecoveryActivityFailureTests {
    [Fact]
    public async Task Confirmed_checkpoint_and_file_lease_knowledge_survive_activity_publication_and_owned_disposal_faults() {
        foreach (var cleanup in new[] { false, true }) {
            foreach (var point in new[] { AgentEffectActivityFault.Report, AgentEffectActivityFault.Suspend,
                    AgentEffectActivityFault.IsTerminal, AgentEffectActivityFault.Dispose,
                    AgentEffectActivityFault.Report | AgentEffectActivityFault.Dispose }) {
                foreach (var currentProfile in new[] { false, true }) {
                    var faults = new AgentEffectActivityFaults(point);
                    var profile = Profile("Owned activity recovery");
                    await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(failLeaseRelease: cleanup,
                        activityDecorator: currentProfile ? null : faults.Decorate,
                        profileBinding: new(profile.Id, profile.Runtime.Fingerprint, new(0)),
                        activityScope: WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")));
                    using var outer = currentProfile ? new AgentEffectCurrentProfileOwner(fixture, profile, faults.Decorate(fixture.Coordinator)) : null;
                    fixture.Receipts.Observe = (_, _) => {
                        faults.Armed = true;
                        return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "private-source"),
                            AgentToolAdmissionJournalFixture.Envelope()));
                    };
                    var failure = await Assert.ThrowsAsync<AgentExecutionActivityPublicationException>(() => outer is null ? fixture.RecoverAsync() :
                        outer.Service.RecoverExecutionRunAsync(fixture.Original.Id, AgentExecutionOperationId.New()));
                    Assert.True(failure.HasEffectOutcome);
                    Assert.Equal(cleanup, failure.EffectLeaseReleaseUnconfirmed);
                    Assert.Equal(point.HasFlag(AgentEffectActivityFault.Dispose), failure.ActivityDisposalFailed);
                    var continuation = Assert.IsType<AgentEffectContinuation>(failure.EffectContinuation);
                    Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, continuation.Persistence);
                    Assert.Equal(fixture.Original.Id, continuation.ExecutionRunId);
                    Assert.Equal(1, continuation.Counts.KnownCommitted);
                    Assert.True(Assert.Single(continuation.Observations).Persisted);
                    if (cleanup) {
                        var original = Assert.Single(Flatten(failure).OfType<AgentEffectReconciliationCleanupException>());
                        Assert.Same(original.Continuation, continuation);
                    }
                    AssertFaultsRetained(faults, failure);
                    var saved = (await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id))!;
                    Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, saved.Run.ToolAdmission!.Batches[0].Proposals[0].State);
                    Assert.Equal(ExecutionState.WaitingOnTool, saved.Run.State);
                    Assert.Empty(saved.Metrics);
                    Assert.Empty(saved.ChatSession!.Messages);
                    Assert.Equal(0, fixture.Runtime.Calls);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await using var released = await fixture.Store.AcquireToolDispatchLeaseAsync(fixture.Original.Id, timeout.Token);
                }
            }
        }
    }

    [Fact]
    public async Task Incomplete_denied_unavailable_cancelled_and_refused_recovery_keep_exact_knowledge_when_activity_finalization_also_fails() {
        foreach (var outcome in Enum.GetValues<Outcome>()) {
            foreach (var cleanup in new[] { false, true }) {
                foreach (var point in new[] { AgentEffectActivityFault.FailOrCancel, AgentEffectActivityFault.IsTerminal,
                        AgentEffectActivityFault.Dispose, AgentEffectActivityFault.FailOrCancel | AgentEffectActivityFault.Dispose }) {
                    var faults = new AgentEffectActivityFaults(point);
                    var armedCheckpoint = false;
                    await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(failLeaseRelease: cleanup,
                        activityDecorator: faults.Decorate, fault: stage => {
                            if (armedCheckpoint && stage == ExistingRunDetailCommitStage.RunPersisted) {
                                armedCheckpoint = false;
                                throw new IOException("Private real checkpoint acknowledgement loss.");
                            }
                        });
                    if (outcome is Outcome.Denied or Outcome.Unavailable) {
                        fixture.Resolver.Change = authority => {
                            faults.Armed = true;
                            if (outcome == Outcome.Unavailable) {
                                throw new IOException("Private current authority read failure.");
                            }
                            return new(authority.AuthorityId, authority.AgentId, authority.DatabaseProfileId,
                                authority.DatabaseProfileGeneration, authority.WorkspaceScope, false, false,
                                authority.PolicyVersion, authority.PolicyFingerprint, authority.ResolvedAtUtc);
                        };
                    } else {
                        fixture.Receipts.Observe = (_, _) => {
                            faults.Armed = true;
                            if (outcome == Outcome.Cancelled) {
                                throw new OperationCanceledException("Private current receipt cancellation.");
                            }
                            armedCheckpoint = true;
                            return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "private-source"),
                                AgentToolAdmissionJournalFixture.Envelope()));
                        };
                    }
                    var failure = await Assert.ThrowsAsync<AgentExecutionActivityPublicationException>(() => fixture.RecoverAsync());
                    Assert.True(failure.HasEffectOutcome);
                    Assert.Equal(cleanup, failure.EffectLeaseReleaseUnconfirmed);
                    Assert.Equal(point.HasFlag(AgentEffectActivityFault.Dispose), failure.ActivityDisposalFailed);
                    if (outcome == Outcome.Incomplete) {
                        var original = Flatten(failure).OfType<AgentEffectReconciliationIncompleteException>().First();
                        Assert.Same(original.Continuation, failure.EffectContinuation);
                        Assert.Equal(AgentEffectCheckpointPersistence.Unconfirmed, failure.EffectContinuation!.Persistence);
                        Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(failure.EffectContinuation.Observations).Knowledge);
                        Assert.False(failure.EffectContinuation.Observations[0].Persisted);
                    } else {
                        Assert.Null(failure.EffectContinuation);
                        Assert.Equal(outcome switch {
                            Outcome.Denied => AgentEffectReconciliationFailureKind.ReadDenied,
                            Outcome.Unavailable => AgentEffectReconciliationFailureKind.ReadUnavailable,
                            Outcome.Cancelled => AgentEffectReconciliationFailureKind.Cancelled,
                            _ => throw new ArgumentOutOfRangeException()
                        }, failure.EffectFailureKind);
                        Assert.Contains(Flatten(failure), exception => outcome switch {
                            Outcome.Denied => exception is AgentToolReceiptAccessDeniedException,
                            Outcome.Unavailable => exception is AgentToolReceiptReadUnavailableException,
                            Outcome.Cancelled => exception is OperationCanceledException,
                            _ => false
                        });
                    }
                    AssertFaultsRetained(faults, failure);
                    Assert.Equal(0, fixture.Runtime.Calls);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await using var released = await fixture.Store.AcquireToolDispatchLeaseAsync(fixture.Original.Id, timeout.Token);
                }
            }
        }
        foreach (var currentProfile in new[] { false, true }) {
            var faults = new AgentEffectActivityFaults(AgentEffectActivityFault.FailOrCancel | AgentEffectActivityFault.Dispose);
            var profile = Profile("Owned refused recovery");
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(run => run with {
                State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = DateTimeOffset.UtcNow
            }, activityDecorator: currentProfile ? null : faults.Decorate,
                profileBinding: new(profile.Id, profile.Runtime.Fingerprint, new(0)),
                activityScope: WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")));
            using var outer = currentProfile ? new AgentEffectCurrentProfileOwner(fixture, profile, faults.Decorate(fixture.Coordinator)) : null;
            faults.Armed = true;
            var failure = await Assert.ThrowsAsync<AgentExecutionActivityPublicationException>(() => outer is null ? fixture.RecoverAsync() :
                outer.Service.RecoverExecutionRunAsync(fixture.Original.Id, AgentExecutionOperationId.New()));
            Assert.True(failure.HasEffectOutcome);
            Assert.Equal(AgentEffectReconciliationFailureKind.AdmissionRejected, failure.EffectFailureKind);
            Assert.Null(failure.EffectContinuation);
            Assert.False(failure.EffectLeaseReleaseUnconfirmed);
            var marked = Flatten(failure).OfType<AgentToolAdmissionException>().First();
            var original = Assert.IsType<AgentToolAdmissionException>(marked.InnerException);
            Assert.Equal("tool-admission.cancelled-reconciliation-required", original.Code);
            Assert.Equal(original.Code, marked.Code);
            AssertFaultsRetained(faults, failure);
            Assert.Equal(fixture.Original.Revision, (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.Revision);
            Assert.Equal(0, fixture.Receipts.Calls);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
        foreach (var admission in new[] { false, true }) {
            var faults = new AgentEffectActivityFaults(AgentEffectActivityFault.FailOrCancel | AgentEffectActivityFault.Dispose);
            var profile = Profile("Owned synchronous recovery");
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(
                profileBinding: new(profile.Id, profile.Runtime.Fingerprint, new(0)),
                activityScope: WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")));
            Exception primary = admission ? new AgentToolAdmissionException("fixture.admission-refused", "Private admission refusal.").ForEffectReconciliation() :
                new AgentToolReceiptAccessDeniedException();
            using var outer = new AgentEffectCurrentProfileOwner(fixture, profile, faults.Decorate(fixture.Coordinator),
                primary, () => faults.Armed = true);
            var failure = await Assert.ThrowsAsync<AgentExecutionActivityPublicationException>(() =>
                outer.Service.RecoverExecutionRunAsync(fixture.Original.Id, AgentExecutionOperationId.New()));
            Assert.Contains(Flatten(failure), retained => ReferenceEquals(primary, retained));
            Assert.Equal(admission ? AgentEffectReconciliationFailureKind.AdmissionRejected : AgentEffectReconciliationFailureKind.ReadDenied,
                failure.EffectFailureKind);
            Assert.Null(failure.EffectContinuation);
            Assert.False(failure.EffectLeaseReleaseUnconfirmed);
            AssertFaultsRetained(faults, failure);
            Assert.Equal(fixture.Original.Revision, (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.Revision);
            Assert.Equal(0, fixture.Receipts.Calls);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
    }

    private static void AssertFaultsRetained(AgentEffectActivityFaults faults, Exception failure) {
        Assert.NotEmpty(faults.Failures);
        Assert.Equal(faults.Failures.Count, faults.Faults);
        Assert.Equal(1, faults.DisposeAttempts);
        Assert.True(faults.ActualDisposed);
        var retained = Flatten(failure).ToArray();
        Assert.All(faults.Failures, expected => Assert.Contains(retained, actual => ReferenceEquals(expected, actual)));
    }

    private static DatabaseProfileRecord Profile(string displayName) {
        var id = Guid.NewGuid();
        return new() {
            Id = id, DisplayName = displayName, ProviderKind = DatabaseProviderKind.InMemory, SourceKind = DatabaseProfileSourceKind.InMemory,
            InMemory = new() { DatabaseName = $"effect_activity_{id:N}" },
            Runtime = new() { Fingerprint = $"owned-effect-activity:{id:N}" },
            Storage = new() { Mode = DatabaseProfileStorageMode.Ephemeral }
        };
    }

    private static IEnumerable<Exception> Flatten(Exception exception) {
        yield return exception;
        IEnumerable<Exception> nested = exception is AggregateException aggregate ? aggregate.InnerExceptions :
            exception.InnerException is { } inner ? [inner] : Enumerable.Empty<Exception>();
        foreach (var item in nested) {
            foreach (var child in Flatten(item)) {
                yield return child;
            }
        }
    }

    private enum Outcome { Incomplete, Denied, Unavailable, Cancelled }
}
