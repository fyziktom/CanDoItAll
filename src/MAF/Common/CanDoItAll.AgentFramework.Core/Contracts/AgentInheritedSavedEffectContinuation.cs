using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedSavedEffectContinuation {
    private readonly Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent;

    public AgentInheritedSavedEffectContinuation(AgentPlanningInheritedOrigin original,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(requireOriginalParent);
        _ = original.ResolvePolicy();
        Original = original;
        this.requireOriginalParent = requireOriginalParent;
    }

    public AgentPlanningInheritedOrigin Original { get; }

    internal void RequireOriginal(ExecutionRunRecord run) {
        if (run.ToolAdmission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
            run.ToolAdmission.PlanningDecision?.InheritedOrigin != Original ||
            run.ToolAdmission.Session.Profile != Original.Profile || run.AgentId != Original.AgentId ||
            run.ToolAdmission.Session.Reference.BackgroundSource != Original.Source) {
            throw new AgentToolAdmissionException("tool-admission.inherited-continuation-changed",
                "The saved effect no longer has its exact original parent, source, material and grant.");
        }
        _ = AgentPlanningRunMetadata.Read(run);
    }

    internal async Task RequireCurrentAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        RequireOriginal(run);
        cancellationToken.ThrowIfCancellationRequested();
        await requireOriginalParent(run, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
