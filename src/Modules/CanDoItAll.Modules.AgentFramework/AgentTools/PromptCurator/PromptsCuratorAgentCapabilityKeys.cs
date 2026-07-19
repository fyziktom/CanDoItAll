using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.AgentFramework;

public static class PromptsCuratorAgentCapabilityKeys
{
    public const string CuratorSkill = "prompts-curator-agent-inline-skill";
    public const string CatalogSearch = "prompt-gallery-catalog-search";
    public const string ItemEditorGet = "prompt-gallery-item-editor-get";
    public const string DraftCreate = "prompt-gallery-draft-create";
    public const string DraftUpdate = "prompt-gallery-draft-update";
    public const string VersionCreate = "prompt-gallery-version-create";

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch] = CatalogSearch,
            [AgentToolInvocationPolicyMetadata.PromptGalleryItemEditorGet] = ItemEditorGet,
            [AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate] = DraftCreate,
            [AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate] = DraftUpdate,
            [AgentToolInvocationPolicyMetadata.PromptGalleryVersionCreate] = VersionCreate
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> PrivilegedKeys { get; } = ToolNameToCapabilityKey.Values
        .Append(CuratorSkill)
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
