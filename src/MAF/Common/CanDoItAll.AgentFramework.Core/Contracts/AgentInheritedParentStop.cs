using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedParentStop {
    private readonly Func<ExecutionRunRecord, CancellationToken, Task> requireStoppedParent;

    public AgentInheritedParentStop(AgentPlanningInheritedOrigin original,
        Func<ExecutionRunRecord, CancellationToken, Task> requireStoppedParent) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(requireStoppedParent);
        _ = original.ResolvePolicy();
        Original = original;
        this.requireStoppedParent = requireStoppedParent;
    }

    public AgentPlanningInheritedOrigin Original { get; }

    internal void RequireOriginal(ExecutionRunRecord run) {
        if (run.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: { } origin } journal || origin != Original ||
                journal.Session.Reference.BackgroundSource != Original.Source || journal.Session.AgentId != Original.AgentId) {
            throw new AgentToolAdmissionException("planning.parent-stop-original-changed",
                "Parent stopping requires the exact original inherited execution, source, material and grant.");
        }
        journal.Validate();
        _ = AgentPlanningRunMetadata.Read(run);
        AgentBackgroundExecutionIdentity.Require(journal.Session, run, Original.Source, Original.Profile);
    }

    internal async Task RequireCurrentAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        RequireOriginal(run);
        cancellationToken.ThrowIfCancellationRequested();
        await requireStoppedParent(run, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
