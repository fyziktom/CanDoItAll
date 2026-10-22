using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public sealed class AgentFrameworkProcessExecutionStopper(ISandboxWorkspaceExecutionRunStore runs,
    AgentBackgroundExecutionStopper stopper, IProcessOriginalReceiptAuthorityReader originals,
    IProcessRootCancellationBarrierReader roots, ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations) : IProcessExecutionStopper {
    public async Task<ProcessCancellationRecoveryPending> StopAsync(ProcessRunId rootRunId, ProcessRunId runId,
        ProcessExecutionRunId executionRunId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(rootRunId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId.Value, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();
        var profileId = database.Profile.Profile.Id;
        var fingerprint = database.Profile.Profile.Runtime.Fingerprint;
        var generation = generations.GetGeneration();
        void RequireProfile() {
            if (database.Profile.Profile.Id != profileId || database.Profile.Profile.Runtime.Fingerprint != fingerprint ||
                    generations.GetGeneration() != generation) {
                throw new InvalidDataException("Process stopping requires a stable current database profile and generation.");
            }
        }
        async Task RequireStoppedRootAsync(CancellationToken token) {
            RequireProfile();
            if (!await roots.IsStoppedAsync(rootRunId, token).ConfigureAwait(false)) {
                throw new InvalidOperationException("Stopping a Process Agent requires its durable original root cancellation barrier.");
            }
            RequireProfile();
        }
        try {
            await RequireStoppedRootAsync(cancellationToken).ConfigureAwait(false);
            var original = (await runs.GetExecutionRunDetailAsync(executionRunId.Value, cancellationToken).ConfigureAwait(false))?.Run;
            if (original?.ToolAdmission is not { } journal) {
                return ProcessCancellationRecoveryPending.MissingJournal;
            }
            if (journal.Session.Profile.ProfileId != profileId || journal.Session.Profile.Fingerprint != fingerprint) {
                throw new InvalidDataException("The original Process Agent belongs to a different database profile.");
            }
            var parent = new AgentBackgroundParentStop(journal.Session, async (current, token) => {
                await RequireStoppedRootAsync(token).ConfigureAwait(false);
                var evidence = ProcessExecutionClaimEvidenceReader.Read(current, out _)
                    ?? throw new InvalidDataException("The original Process Agent has no retained claim evidence.");
                if (evidence.RunId != runId || evidence.ExecutionRunId != executionRunId.Value) {
                    throw new InvalidDataException("The selected Process run differs from the original Agent execution.");
                }
                var result = await originals.ObserveOriginalForReceiptReadAsync(evidence, token).ConfigureAwait(false);
                if (result.Disposition != ProcessExecutionAuthorityDisposition.Bound || result.Snapshot is not { } snapshot ||
                        snapshot.Evidence != evidence || snapshot.RootRunId != rootRunId ||
                        snapshot.SourceAuthority?.DatabaseProfileId != profileId ||
                        AgentToolProtocolEnvelope.ComputeDigest(snapshot.OwnerFingerprint) != journal.Session.Reference.BackgroundSource!.OwnerFingerprint) {
                    throw new InvalidDataException("The original Process receipt no longer matches the selected root, profile and admitted source.");
                }
                RequireProfile();
            });
            var outcome = await stopper.StopAsync(executionRunId.Value, parent, cancellationToken).ConfigureAwait(false);
            return outcome switch {
                AgentParentStopOutcome.Stopped or AgentParentStopOutcome.AlreadyClosed => ProcessCancellationRecoveryPending.None,
                AgentParentStopOutcome.Signalled or AgentParentStopOutcome.OpenWork => ProcessCancellationRecoveryPending.ActiveWork,
                AgentParentStopOutcome.LeaseBusy => ProcessCancellationRecoveryPending.LeaseBusy,
                AgentParentStopOutcome.MissingJournal => ProcessCancellationRecoveryPending.MissingJournal,
                AgentParentStopOutcome.UnresolvedEffects => ProcessCancellationRecoveryPending.UnresolvedEffects,
                AgentParentStopOutcome.CompletionPending => ProcessCancellationRecoveryPending.CompletionPending,
                _ => throw new InvalidDataException("The ordinary Agent stop owner returned an unsupported outcome.")
            };
        } finally {
            RequireProfile();
        }
    }
}
