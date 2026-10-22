using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessChildStopper(ISandboxWorkspaceExecutionRunStore runs,
    AgentInheritedPlanningStopper stopper, IProcessOriginalReceiptAuthorityReader originals,
    IProcessRootCancellationBarrierReader roots, ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations) : IProcessDecisionChildStopper {
    public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;

    public async Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var profile = database.Profile.Profile;
        var generation = generations.GetGeneration();
        void RequireProfile() {
            if (original.ExecutionContract != Contract || original.Scope.DatabaseProfileId != profile.Id ||
                    database.Profile.Profile.Id != profile.Id || database.Profile.Profile.Runtime.Fingerprint != profile.Runtime.Fingerprint ||
                    generations.GetGeneration() != generation) {
                throw new InvalidDataException("Parent stopping requires the original profile and a stable current profile generation.");
            }
        }
        async Task RequireStoppedRootAsync(CancellationToken token) {
            RequireProfile();
            if (!await roots.IsStoppedAsync(original.Child.Address.RootRunId, token).ConfigureAwait(false)) {
                throw new InvalidOperationException("Stopping an inherited Agent requires its durable original root cancellation barrier.");
            }
            RequireProfile();
        }
        try {
            await RequireStoppedRootAsync(cancellationToken).ConfigureAwait(false);
            var run = await runs.GetExecutionRunAsync(original.Child.Address.ChildId.Value, cancellationToken).ConfigureAwait(false);
            if (run?.ToolAdmission is null) {
                return ProcessDecisionChildStopResult.MissingJournal;
            }
            var (target, _) = ProjectTaskPlanProcessOutcome.Read(run, original);
            var origin = run.ToolAdmission.PlanningDecision!.InheritedOrigin!;
            if (target.Session.Profile.ProfileId != profile.Id || target.Session.Profile.Fingerprint != profile.Runtime.Fingerprint) {
                throw new InvalidDataException("The original inherited Agent belongs to a different database profile.");
            }
            var parent = new AgentInheritedParentStop(origin, async (current, token) => {
                _ = ProjectTaskPlanProcessOutcome.Read(current, original);
                await RequireStoppedRootAsync(token).ConfigureAwait(false);
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
