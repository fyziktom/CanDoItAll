using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessChildStopper(ISandboxWorkspaceExecutionRunStore runs,
    AgentInheritedPlanningStopper stopper, IProcessOriginalReceiptAuthorityReader originals,
    IProcessRootCancellationBarrierReader roots, ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations, IProcessDecisionRefreshAuthorityReader refreshes)
    : IProcessDecisionChildStopper, IProcessDecisionRefreshChildStopper {
    public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;

    public Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original,
        CancellationToken cancellationToken = default)
        => StopCoreAsync(original, async token => {
            if (!await roots.IsStoppedAsync(original.Child.Address.RootRunId, token).ConfigureAwait(false)) {
                throw new InvalidOperationException("Stopping an inherited Agent requires its durable original root cancellation barrier.");
            }
        }, cancellationToken);

    Task<ProcessDecisionChildStopResult> IProcessDecisionRefreshChildStopper.StopAsync(ProcessDecisionChildMaterialBinding original,
        RuntimeCommandId admissionId, CancellationToken cancellationToken)
        => StopCoreAsync(original, token => refreshes.RequireCurrentAsync(original, admissionId, token), cancellationToken);

    private async Task<ProcessDecisionChildStopResult> StopCoreAsync(ProcessDecisionChildMaterialBinding original,
        Func<CancellationToken, Task> requireParent, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var profileId = database.Profile.Profile.Id;
        var fingerprint = database.Profile.Profile.Runtime.Fingerprint;
        var generation = generations.GetGeneration();
        void RequireProfile() {
            if (original.ExecutionContract != Contract || original.Scope.DatabaseProfileId != profileId ||
                    database.Profile.Profile.Id != profileId || database.Profile.Profile.Runtime.Fingerprint != fingerprint ||
                    generations.GetGeneration() != generation) {
                throw new InvalidDataException("Parent stopping requires the original profile and a stable current profile generation.");
            }
        }
        async Task RequireParentAsync(CancellationToken token) {
            RequireProfile();
            await requireParent(token).ConfigureAwait(false);
            RequireProfile();
        }
        try {
            await RequireParentAsync(cancellationToken).ConfigureAwait(false);
            var run = await runs.GetExecutionRunAsync(original.Child.Address.ChildId.Value, cancellationToken).ConfigureAwait(false);
            if (run?.ToolAdmission is null) {
                return ProcessDecisionChildStopResult.MissingJournal;
            }
            var (target, _) = ProjectTaskPlanProcessOutcome.Read(run, original);
            var origin = run.ToolAdmission.PlanningDecision!.InheritedOrigin!;
            if (target.Session.Profile.ProfileId != profileId || target.Session.Profile.Fingerprint != fingerprint) {
                throw new InvalidDataException("The original inherited Agent belongs to a different database profile.");
            }
            var parent = new AgentInheritedParentStop(origin, async (current, token) => {
                _ = ProjectTaskPlanProcessOutcome.Read(current, original);
                await RequireParentAsync(token).ConfigureAwait(false);
                var source = await originals.ObserveOriginalDecisionForReceiptReadAsync(target.Source.Evidence, original,
                    Contract, ProjectTaskPlanProcessContract.Content, token).ConfigureAwait(false);
                target.RequireOriginalReceiptSource(source, origin);
                RequireProfile();
            });
            var outcome = await stopper.StopAsync(run.Id, parent, cancellationToken).ConfigureAwait(false);
            return outcome switch {
                AgentParentStopOutcome.Stopped or AgentParentStopOutcome.AlreadyClosed => ProcessDecisionChildStopResult.ReadyForAccounting,
                AgentParentStopOutcome.Signalled or AgentParentStopOutcome.OpenWork => ProcessDecisionChildStopResult.ActiveWork,
                AgentParentStopOutcome.LeaseBusy => ProcessDecisionChildStopResult.LeaseBusy,
                AgentParentStopOutcome.MissingJournal => ProcessDecisionChildStopResult.MissingJournal,
                AgentParentStopOutcome.UnresolvedEffects => ProcessDecisionChildStopResult.UnresolvedEffects,
                AgentParentStopOutcome.CompletionPending => ProcessDecisionChildStopResult.CompletionPending,
                _ => throw new InvalidDataException("The inherited Agent stop owner returned an unsupported result.")
            };
        } finally {
            RequireProfile();
        }
    }
}
