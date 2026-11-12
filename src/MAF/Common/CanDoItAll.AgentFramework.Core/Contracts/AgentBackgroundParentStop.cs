using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentBackgroundParentStop {
    private readonly Func<ExecutionRunRecord, CancellationToken, Task> requireStoppedParent;

    public AgentBackgroundParentStop(AgentToolSessionAdmission original,
        Func<ExecutionRunRecord, CancellationToken, Task> requireStoppedParent) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(requireStoppedParent);
        if (original.Reference.BackgroundSource is null || original.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation) {
            throw new ArgumentException("Parent stopping requires an original admitted background session.", nameof(original));
        }
        Original = original;
        this.requireStoppedParent = requireStoppedParent;
    }

    public AgentToolSessionAdmission Original { get; }

    internal void RequireOriginal(ExecutionRunRecord run) {
        if (run.ToolAdmission is not { PlanningDecision: null } journal || journal.Session != Original ||
                journal.SchemaVersion is not (AgentToolJournalRecord.BackgroundSchemaVersion or
                    AgentToolJournalRecord.ProviderDispatchSchemaVersion or AgentToolJournalRecord.EffectJournalSchemaVersion)) {
            throw new AgentToolAdmissionException("background.parent-stop-original-changed",
                "Parent stopping requires the exact original ordinary background journal and session.");
        }
        journal.Validate();
        AgentBackgroundExecutionIdentity.Require(journal.Session, run, Original.Reference.BackgroundSource!, Original.Profile);
    }

    internal async Task RequireCurrentAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        RequireOriginal(run);
        cancellationToken.ThrowIfCancellationRequested();
        await requireStoppedParent(run, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
