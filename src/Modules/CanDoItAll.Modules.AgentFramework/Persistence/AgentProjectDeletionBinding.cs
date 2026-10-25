using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.AgentFramework;

internal static class AgentProjectDeletionBinding {
    internal const int ProtocolVersion = 1;
    internal static ProjectDeletionPreparationContext? Read(AgentProjectStructureAccessRevocationRecord record, Guid databaseProfileId) {
        if (record.CompletionNamespaceId is null && record.CompletionId is null) {
            return null;
        }
        if (record.DatabaseProfileId != databaseProfileId || record.ProjectLifetimeId is not { } lifetime ||
                record.CompletionNamespaceId is not { } ns || record.CompletionId is not { } completion) {
            throw new ProjectDeletionParticipantCleanupException(record.Id,
                "The retained project-access cleanup has an inconsistent original completion binding.");
        }
        return new(new(record.DatabaseProfileId.Value, new(ns), new(completion)),
            new(record.DatabaseProfileId.Value, record.ProjectId, lifetime));
    }
}
