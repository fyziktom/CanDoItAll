using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessOutcomeReader(ISandboxWorkspaceHistoricalReadStore runs,
    IProcessOriginalReceiptAuthorityReader originals, ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations) : IProcessDecisionChildOutcomeReader {
    public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;

    public async Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var profile = database.Profile.Profile;
        var generation = generations.GetGeneration();
        void RequireProfile() {
            if (original.ExecutionContract != Contract || original.Scope.DatabaseProfileId != profile.Id ||
                database.Profile.Profile.Id != profile.Id || database.Profile.Profile.Runtime.Fingerprint != profile.Runtime.Fingerprint ||
                generations.GetGeneration() != generation) {
                throw new InvalidDataException("Process outcome accounting requires the original database profile and execution contract.");
            }
        }
        RequireProfile();
        cancellationToken.ThrowIfCancellationRequested();
        try {
            var observed = await runs.ReadHistoricalRunAsync(original.Child.Address.ChildId.Value, cancellationToken);
            if (observed.Status == AgentHistoricalStorageReadStatus.Missing) {
                return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.MissingJournal);
            }
            if (observed.Status != AgentHistoricalStorageReadStatus.Ready) {
                throw new IOException($"Process child accounting requires a stable retained run: {observed.Status}.");
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
            if (result.OriginalDecisionPolicy != run.ToolAdmission.PlanningDecision!.InheritedOrigin!.ParentPolicy ||
                result.Snapshot is not { ObservedCurrentDispatch: false, SourceAuthority: { } authority } current ||
                current.Evidence != target.Source.Evidence || current.OwnerFingerprint != target.Source.OwnerFingerprint ||
                current.RootRunId != target.Source.RootRunId || current.ProjectReference != target.Source.ProjectReference ||
                JsonSerializer.Serialize(authority) != JsonSerializer.Serialize(target.Source.SourceAuthority)) {
                throw new InvalidDataException("The retained child outcome differs from its original accepted Process source.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return outcome;
        } finally {
            RequireProfile();
        }
    }
}
