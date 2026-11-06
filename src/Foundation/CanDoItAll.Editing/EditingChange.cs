namespace CanDoItAll.Editing;

public enum EditingOwner { PromptGallery, CrmHrParty, CrmAccountProfile, CrmHrAffiliations, CrmHrRelationships, CrmOpportunity, CrmAccountRecords, CrmWorkforceProfile, CrmWorkforceRecords, CrmSkillCatalog, CrmRecruitmentApplication, CrmRecruitmentInterviews, CrmRecruitmentTasks, CrmAiStewardProfile, CrmStaffingRequest, CrmStaffingRecords, ProjectAssignment, ProjectAssignmentRecords, Project, ProjectStructure, WorkflowDefinition, WorkflowComponent, ProcessAuthoring, AgentDefinition, AgentTeam, AgentCapability, ProviderProfile, SharedProviderSource, SimpleChatDefinition, SimpleChatConversation, AgentVoiceSettings, FloatingChatSettings, AgentVoicePreview }
public enum EditingScopeKind { Instance, Dataset }
public enum EditingChangeKind { Created, EditableContentChanged, LifecycleChanged, MetadataChanged, Deleted, AccessChanged }

public readonly record struct EditingScope(EditingScopeKind Kind, Guid Id, long ActivationGeneration = 0);
public readonly record struct EditingTopic(EditingScope Scope, EditingOwner Owner, Guid? ResourceId = null);
public readonly record struct EditingCursor(Guid Epoch, long Sequence);

public sealed record EditingChange(EditingCursor Cursor, EditingTopic Resource, EditingChangeKind Kind,
    Guid? OriginEditorId, Guid OperationId, DateTimeOffset OccurredAtUtc);

public sealed record EditingChangeBatch(IReadOnlyList<EditingChange> Changes, bool RequiresResync);

public interface IEditingChangeSubscription : IDisposable {
    EditingCursor JoinedAt { get; }
    ValueTask<EditingChangeBatch> ReadAsync(CancellationToken cancellationToken);
}

public interface IEditingChangeObserver {
    IEditingChangeSubscription Subscribe(EditingTopic topic);
}

public interface IEditingChangePublisher {
    void Publish(EditingTopic resource, EditingChangeKind kind, Guid? originEditorId = null, Guid? operationId = null);
}
