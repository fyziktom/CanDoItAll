using System.Text.Json;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectWorkbenchDeletionPayload {
    internal const int RecoveryProtocolVersion = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static DeleteProjectMutationPayload Read(ProjectCrossModuleMutationRecord record)
        => Read(record.PayloadJson, record.CompletionNamespaceId, record.CompletionId);

    internal static DeleteProjectMutationPayload Read(string payloadJson, Guid? completionNamespaceId = null, Guid? completionId = null) {
        var payload = JsonSerializer.Deserialize<DeleteProjectMutationPayload>(payloadJson, Json)
            ?? throw new InvalidDataException("Unable to deserialize the durable Workbench project-deletion payload.");
        if (completionNamespaceId is null && completionId is null) {
            return payload;
        }
        if (completionNamespaceId is not { } ns || completionId is not { } id ||
                payload.SourceReference is not { } source) {
            throw new InvalidDataException("The retained Workbench deletion has an incomplete completion binding.");
        }
        var original = new ProjectDeletionPreparationContext(new(source.DatabaseProfileId, new(ns), new(id)),
            source.RequireBoundAdmission());
        if (payload.CompletionContext is not null && payload.CompletionContext != original) {
            throw new InvalidDataException("The retained Workbench deletion header and payload completion bindings disagree.");
        }
        return payload with { CompletionContext = original };
    }

    internal static bool Matches(DeleteProjectMutationPayload payload, ProjectDeletionPreparationContext original)
        => payload.CompletionContext == original && payload.SourceReference == ProjectAssignmentReference.From(original.Project) &&
            payload.DeletedNodeKeys is not null && payload.ManagedStorageObjects is not null;
}
