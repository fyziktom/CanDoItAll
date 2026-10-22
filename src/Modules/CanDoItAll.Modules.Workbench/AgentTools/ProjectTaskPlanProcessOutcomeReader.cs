using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessOutcomeReader(ISandboxWorkspaceHistoricalReadStore runs,
    IProcessOriginalReceiptAuthorityReader originals, ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations) : IProcessDecisionLiveOutcomeReader {
    public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;

    public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original,
        CancellationToken cancellationToken = default)
        => ReadAsync(original, null, cancellationToken);

    public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionResultChild original, ProcessExecutionOwnerEvidence evidence,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(evidence);
        var profile = database.Profile.Profile;
        AgentToolProfileBinding binding = new(profile.Id, profile.Runtime.Fingerprint, generations.GetGeneration());
        var closed = ProjectTaskPlanProcessStartupOutcome.Read(original, evidence, binding);
        return ReadAsync(original.Original, (closed, binding), cancellationToken);
    }

    private async Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original,
        (ProcessDecisionChildResult.Closed Outcome, AgentToolProfileBinding Profile)? liveFailure, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var profile = database.Profile.Profile;
        var generation = generations.GetGeneration();
        void RequireProfile() {
            if (original.ExecutionContract != Contract || original.Scope.DatabaseProfileId != profile.Id ||
                database.Profile.Profile.Id != profile.Id || database.Profile.Profile.Runtime.Fingerprint != profile.Runtime.Fingerprint ||
                generations.GetGeneration() != generation ||
                liveFailure is { } live && live.Profile != new AgentToolProfileBinding(profile.Id, profile.Runtime.Fingerprint, generation)) {
                throw new InvalidDataException("Process outcome accounting requires the original database profile and execution contract.");
            }
        }
        RequireProfile();
        cancellationToken.ThrowIfCancellationRequested();
        try {
            var observed = await runs.ReadHistoricalRunAsync(original.Child.Address.ChildId.Value, cancellationToken);
            if (observed.Status == AgentHistoricalStorageReadStatus.Missing) {
                cancellationToken.ThrowIfCancellationRequested();
                return liveFailure?.Outcome ?? (ProcessDecisionChildResult)new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.MissingJournal);
            }
            if (observed.Status != AgentHistoricalStorageReadStatus.Ready) {
                throw new IOException($"Process child accounting requires a stable retained run: {observed.Status}.");
            }
            if (liveFailure is not null) {
                return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.UnresolvedEffects);
            }
            if (observed.Snapshot is not { Chat: null, Run: { } run }) {
                throw new InvalidDataException("Process child accounting requires the original background run snapshot.");
            }
            if (run.ToolAdmission is null) {
                return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.MissingJournal);
            }
            var (target, outcome) = ProjectTaskPlanProcessOutcome.Read(run, original);
            if (target.Session.Profile.ProfileId != profile.Id || target.Session.Profile.Fingerprint != profile.Runtime.Fingerprint) {
                throw new InvalidDataException("The retained Process child outcome belongs to a different database profile.");
            }
            var result = await originals.ObserveOriginalDecisionForReceiptReadAsync(target.Source.Evidence, original,
                Contract, ProjectTaskPlanProcessContract.Content, cancellationToken);
            target.RequireOriginalReceiptSource(result, run.ToolAdmission.PlanningDecision!.InheritedOrigin!);
            cancellationToken.ThrowIfCancellationRequested();
            return outcome;
        } finally {
            RequireProfile();
        }
    }
}
