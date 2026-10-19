using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentPlanningPublicationExpectation {
    public AgentPlanningPublicationExpectation(WorkspaceScopeDescriptor scope, AgentToolSessionAdmission session,
        long runRevision, long journalRevision) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(session.Reference);
        ArgumentNullException.ThrowIfNull(session.Profile);
        if (session.AgentId == Guid.Empty || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
                session.Reference.BackgroundSource is not null || runRevision <= 0 || journalRevision <= 0 ||
                scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N"))) {
            throw new ArgumentException("A publication snapshot requires its original interactive organization, session and revisions.");
        }
        Scope = scope with { };
        Session = session with { };
        RunRevision = runRevision;
        JournalRevision = journalRevision;
    }

    public WorkspaceScopeDescriptor Scope { get; }
    public AgentToolSessionAdmission Session { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
}

public interface IAgentPlanningPublicationSnapshotLease : IAgentCatalogReadLease {
    new AgentDefinition Agent { get; }
    ExecutionRunRecord OriginalRun { get; }
    ChatSessionRecord OriginalChatSession { get; }
    AgentPlanningPublicationExpectation Original { get; }
    void RequireActive();
}
