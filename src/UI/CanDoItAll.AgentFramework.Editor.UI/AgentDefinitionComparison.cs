using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.Editor.UI;

public sealed record AgentComparisonField<T>(string Label, MergeField<T> Value, Action<AgentEditorModel, T> Apply);

public sealed record AgentRuntimeSelection(Guid? ProviderId, string Model, AgentReasoningEffortLevel? ThinkingEffort,
    bool ThinkingEffortEdited);

public sealed class AgentDefinitionComparison {
    public AgentDefinitionComparison(AgentEditorModel original, AgentEditorModel local, AgentEditorModel current) {
        if (original.Id is null || original.Id != local.Id || local.Id != current.Id || current.ExpectedUpdatedAtUtc is null) {
            throw new InvalidOperationException("Agent comparison requires the same saved agent and its current version.");
        }
        Local = AgentEditorDraftSnapshot.Copy(local);
        Current = AgentEditorDraftSnapshot.Copy(current);
        Text = [
            Field("Name", original.Name, local.Name, current.Name, (draft, value) => draft.Name = value),
            Field("Role", original.RoleTitle, local.RoleTitle, current.RoleTitle, (draft, value) => draft.RoleTitle = value),
            Field("Summary", original.Summary, local.Summary, current.Summary, (draft, value) => draft.Summary = value),
            Field("Avatar", original.AvatarImageUrl, local.AvatarImageUrl, current.AvatarImageUrl, (draft, value) => draft.AvatarImageUrl = value),
            Field("Template key", original.TemplateKey, local.TemplateKey, current.TemplateKey, (draft, value) => draft.TemplateKey = value)
        ];
        Instructions = TextMerge.Compare(original.Instructions, local.Instructions, current.Instructions);
        Toggles = [
            Field("Persist each service call", original.RequirePerServiceCallChatHistoryPersistence, local.RequirePerServiceCallChatHistoryPersistence,
                current.RequirePerServiceCallChatHistoryPersistence, (draft, value) => draft.RequirePerServiceCallChatHistoryPersistence = value),
            Field("Background responses", original.EnableBackgroundResponses, local.EnableBackgroundResponses,
                current.EnableBackgroundResponses, (draft, value) => draft.EnableBackgroundResponses = value),
            Field("Template", original.IsTemplate, local.IsTemplate, current.IsTemplate, (draft, value) => draft.IsTemplate = value)
        ];
        Status = Field("Status", original.Status, local.Status, current.Status, (draft, value) => draft.Status = value);
        Workload = Field("Workload", original.Workload, local.Workload, current.Workload, (draft, value) => draft.Workload = value);
        History = Field("Chat history", original.ChatHistoryMode, local.ChatHistoryMode, current.ChatHistoryMode, (draft, value) => draft.ChatHistoryMode = value);
        Temperature = Field("Temperature", original.Temperature, local.Temperature, current.Temperature, (draft, value) => draft.Temperature = value);
        Runtime = new(Selection(original), Selection(local), Selection(current));
        Permissions = Sensitive(original.Permissions, local.Permissions, current.Permissions);
        Secrets = Sensitive<IReadOnlyList<AgentAllowedSecretReference>>(original.AllowedSecretReferences, local.AllowedSecretReferences, current.AllowedSecretReferences);
        Projects = Sensitive(original.ProjectStructureAccess, local.ProjectStructureAccess, current.ProjectStructureAccess);
        Processes = Sensitive(original.ProcessAccess, local.ProcessAccess, current.ProcessAccess);
        Workspace = Sensitive(original.WorkspaceToolAccess, local.WorkspaceToolAccess, current.WorkspaceToolAccess);
        Images = Sensitive(original.ImageGenerationAccess, local.ImageGenerationAccess, current.ImageGenerationAccess);
        Voice = Sensitive(original.VoiceAccess, local.VoiceAccess, current.VoiceAccess);
        Memory = Sensitive(original.MemoryAccess, local.MemoryAccess, current.MemoryAccess);
        Capabilities = Sensitive<IReadOnlyList<Guid>>(original.SelectedCapabilityIds, local.SelectedCapabilityIds, current.SelectedCapabilityIds);
        PreservedConfiguration = new(original.ConfigurationJson, local.ConfigurationJson, current.ConfigurationJson, StringComparer.Ordinal);
        Tags = CollectionMerge.Membership(original.Tags, local.Tags, current.Tags, StringComparer.OrdinalIgnoreCase);
    }

    public AgentEditorModel Local { get; }
    public AgentEditorModel Current { get; }
    public bool IsStale { get; private set; }
    public event Action? Invalidated;

    public void Invalidate() {
        if (IsStale) {
            return;
        }
        IsStale = true;
        Invalidated?.Invoke();
    }
    public IReadOnlyList<AgentComparisonField<string>> Text { get; }
    public TextMergeResult Instructions { get; }
    public IReadOnlyList<AgentComparisonField<bool>> Toggles { get; }
    public AgentComparisonField<AgentLifecycleStatus> Status { get; }
    public AgentComparisonField<AgentWorkloadKind> Workload { get; }
    public AgentComparisonField<AgentChatHistoryMode> History { get; }
    public AgentComparisonField<double> Temperature { get; }
    public MergeField<AgentRuntimeSelection> Runtime { get; }
    public MergeField<AgentPermissionsPolicy> Permissions { get; }
    public MergeField<IReadOnlyList<AgentAllowedSecretReference>> Secrets { get; }
    public MergeField<AgentProjectStructureAccessSettings> Projects { get; }
    public MergeField<AgentProcessAccessSettings> Processes { get; }
    public MergeField<AgentWorkspaceToolAccessSettings> Workspace { get; }
    public MergeField<AgentImageGenerationAccessSettings> Images { get; }
    public MergeField<AgentVoiceAccessSettings> Voice { get; }
    public MergeField<AgentMemoryAccessSettings> Memory { get; }
    public MergeField<IReadOnlyList<Guid>> Capabilities { get; }
    public MergeField<string> PreservedConfiguration { get; }
    public IReadOnlyList<string> Tags { get; }
    public bool IsResolved => Text.All(item => item.Value.IsResolved) && Toggles.All(item => item.Value.IsResolved) &&
        Instructions.Field.IsResolved && Status.Value.IsResolved && Workload.Value.IsResolved && History.Value.IsResolved &&
        Temperature.Value.IsResolved && Runtime.IsResolved && Permissions.IsResolved && Secrets.IsResolved && Projects.IsResolved &&
        Processes.IsResolved && Workspace.IsResolved && Images.IsResolved && Voice.IsResolved && Memory.IsResolved &&
        Capabilities.IsResolved && PreservedConfiguration.IsResolved;

    public AgentEditorModel Result() {
        if (!IsResolved || IsStale) {
            throw new InvalidOperationException("Resolve every changed agent field and permission group before applying.");
        }
        var result = AgentEditorDraftSnapshot.Copy(Local);
        foreach (var field in Text) {
            field.Apply(result, field.Value.Result);
        }
        foreach (var field in Toggles) {
            field.Apply(result, field.Value.Result);
        }
        Status.Apply(result, Status.Value.Result);
        Workload.Apply(result, Workload.Value.Result);
        History.Apply(result, History.Value.Result);
        Temperature.Apply(result, Temperature.Value.Result);
        result.Instructions = Instructions.Field.Result;
        result.ProviderProfileId = Runtime.Result.ProviderId;
        result.Model = Runtime.Result.Model;
        result.ThinkingEffortOverride = Runtime.Result.ThinkingEffort;
        result.IsThinkingEffortOverrideEdited = Runtime.Result.ThinkingEffortEdited;
        result.Permissions = Permissions.Result;
        result.AllowedSecretReferences = Secrets.Result.ToList();
        result.ProjectStructureAccess = Projects.Result;
        result.ProcessAccess = Processes.Result;
        result.WorkspaceToolAccess = Workspace.Result;
        result.ImageGenerationAccess = Images.Result;
        result.VoiceAccess = Voice.Result;
        result.MemoryAccess = Memory.Result;
        result.SelectedCapabilityIds = Capabilities.Result.ToList();
        result.ConfigurationJson = PreservedConfiguration.Result;
        result.Tags = Tags.ToList();
        result.ExpectedUpdatedAtUtc = Current.ExpectedUpdatedAtUtc;
        return AgentEditorDraftSnapshot.Copy(result);
    }

    public static bool Same(AgentEditorModel first, AgentEditorModel second)
        => ValueEquality<AgentEditorModel>.Instance.Equals(first, second);

    private static AgentRuntimeSelection Selection(AgentEditorModel model)
        => new(model.ProviderProfileId, model.Model, model.ThinkingEffortOverride, model.IsThinkingEffortOverrideEdited);

    private static AgentComparisonField<T> Field<T>(string label, T original, T local, T current, Action<AgentEditorModel, T> apply)
        => new(label, new(original, local, current), apply);

    private static MergeField<T> Sensitive<T>(T original, T local, T current) {
        var field = new MergeField<T>(original, local, current, ValueEquality<T>.Instance);
        if (field.CurrentChanged) {
            field.RequireChoice();
        }
        return field;
    }

    private sealed class ValueEquality<T> : IEqualityComparer<T> {
        public static ValueEquality<T> Instance { get; } = new();
        public bool Equals(T? first, T? second) => JsonSerializer.SerializeToUtf8Bytes(first).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(second));
        public int GetHashCode(T value) => StringComparer.Ordinal.GetHashCode(JsonSerializer.Serialize(value));
    }
}
