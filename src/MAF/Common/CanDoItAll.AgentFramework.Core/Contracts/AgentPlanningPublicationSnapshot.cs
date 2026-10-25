using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentPlanningPublicationOrigin {
    Interactive,
    Inherited
}

public sealed record AgentPlanningPublicationExpectation {
    public AgentPlanningPublicationExpectation(WorkspaceScopeDescriptor scope, AgentToolSessionAdmission session,
        long runRevision, long journalRevision)
        : this(scope, session, runRevision, journalRevision, AgentPlanningPublicationOrigin.Interactive, null) { }

    private AgentPlanningPublicationExpectation(WorkspaceScopeDescriptor scope, AgentToolSessionAdmission session,
        long runRevision, long journalRevision, AgentPlanningPublicationOrigin origin, Guid? additionalAgentId) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(session.Reference);
        ArgumentNullException.ThrowIfNull(session.Profile);
        var validSource = origin == AgentPlanningPublicationOrigin.Inherited
            ? session.Purpose == AgentRuntimeContextPurpose.GovernedProcessAutomation && session.Reference.BackgroundSource is not null
            : session.Purpose == AgentRuntimeContextPurpose.InteractiveChat && session.Reference.BackgroundSource is null;
        if (session.AgentId == Guid.Empty || !validSource || runRevision <= 0 || journalRevision <= 0 ||
                additionalAgentId == Guid.Empty || additionalAgentId == session.AgentId ||
                scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N"))) {
            throw new ArgumentException("A publication snapshot requires its original organization, source, session and revisions, and distinct selected Agents.");
        }
        Scope = scope with { };
        Session = session with { };
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        Origin = origin;
        AdditionalAgentId = additionalAgentId;
    }

    public static AgentPlanningPublicationExpectation Inherited(WorkspaceScopeDescriptor scope, AgentToolSessionAdmission session,
        long runRevision, long journalRevision, Guid? additionalAgentId = null)
        => new(scope, session, runRevision, journalRevision, AgentPlanningPublicationOrigin.Inherited, additionalAgentId);

    public WorkspaceScopeDescriptor Scope { get; }
    public AgentToolSessionAdmission Session { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
    public AgentPlanningPublicationOrigin Origin { get; }
    public Guid? AdditionalAgentId { get; }
}

public interface IAgentPlanningPublicationSnapshotLease : IAgentCatalogReadLease {
    new AgentDefinition Agent { get; }
    ExecutionRunRecord OriginalRun { get; }
    ChatSessionRecord? OriginalChatSession { get; }
    AgentPlanningPublicationExpectation Original { get; }
    void RequireActive();
}
