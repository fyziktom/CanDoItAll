using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.AgentFramework;

public static class PromptGalleryToolPolicy {
    public const string PromptGallerySearch = "prompt_gallery_search";
    public const string PromptGalleryItemGet = "prompt_gallery_item_get";
    public const string PromptGalleryCatalogSearch = "prompt_gallery_catalog_search";
    public const string PromptGalleryItemEditorGet = "prompt_gallery_item_editor_get";
    public const string PromptGalleryDraftCreate = "prompt_gallery_draft_create";
    public const string PromptGalleryDraftUpdate = "prompt_gallery_draft_update";
    public const string PromptGalleryVersionCreate = "prompt_gallery_version_create";
    public const string ApprovalAuditRetentionScheme = "prompt-curator-approval-redacted-v1";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Create(PromptGallerySearch),
        Create(PromptGalleryItemGet),
        Create(PromptGalleryCatalogSearch, retainBusinessArguments: true),
        Create(PromptGalleryItemEditorGet),
        Create(PromptGalleryDraftCreate, mutation: true, retainBusinessArguments: true),
        Create(PromptGalleryDraftUpdate, mutation: true, retainBusinessArguments: true),
        Create(PromptGalleryVersionCreate, mutation: true, retainBusinessArguments: true)
    ]);

    public static AgentRuntimeToolMetadata CreateRuntimeMetadata(string providerKey, string toolName, IReadOnlyList<string> tags) {
        var policy = Capabilities.Single(policy => string.Equals(policy.Name, toolName, StringComparison.Ordinal));
        return new AgentRuntimeToolMetadata(
            providerKey,
            policy.Name,
            policy.Classification == ToolInvocationClassification.Mutation
                ? AgentRuntimeToolOperationKind.Mutation
                : AgentRuntimeToolOperationKind.Read,
            policy.RequiresApprovalByDefault,
            tags);
    }

    private static ToolCapabilityMetadata Create(string name, bool mutation = false, bool retainBusinessArguments = false) {
        return new ToolCapabilityMetadata(
            name,
            mutation ? ToolInvocationClassification.Mutation : ToolInvocationClassification.Read,
            RequiresApprovalByDefault: mutation,
            IsStateChanging: mutation,
            mutation ? ToolCapabilitySideEffectKind.InternalStateMutation : ToolCapabilitySideEffectKind.InternalDataRead,
            ToolCapabilityOperationRequirementKind.None,
            OperationRequirements: [],
            TargetScopeRequirements: [],
            CanMutateProduct: false,
            CanExecuteExternalAction: false,
            CanReadExternalTarget: !mutation,
            CanWriteManagedArtifact: false,
            ToolCapabilityBrowserProofRole.None,
            mutation ? ToolCapabilityIdempotencyDescriptor.StateChanging : ToolCapabilityIdempotencyDescriptor.Idempotent) {
            BusinessArgumentRetentionScheme = retainBusinessArguments ? ApprovalAuditRetentionScheme : null
        };
    }
}
