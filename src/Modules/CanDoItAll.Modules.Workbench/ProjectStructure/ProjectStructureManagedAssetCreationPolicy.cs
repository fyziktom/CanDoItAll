using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureManagedAssetCreationPolicy
{
    internal const string AssetParentRequiredErrorCode = "AssetParentRequired";
    internal const string ManagedAssetCreationRequiredErrorCode = "ManagedAssetCreationRequired";
    private const string MermaidObjectSubtype = "mermaid";

    internal static void EnsureExplicitParent(string? parentNodeKey)
    {
        if (!string.IsNullOrWhiteSpace(parentNodeKey))
        {
            return;
        }

        throw ProjectStructureAgentException.CreateAgentVisible(
            400,
            AssetParentRequiredErrorCode,
            "Managed asset creation requires an explicit parentNodeKey.",
            canRetryWithCorrectedInput: true);
    }

    internal static void EnsureGenericNodeCreateAllowed(
        ProjectObjectType objectType,
        string? objectSubtype)
    {
        var normalizedSubtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(
            objectType,
            objectSubtype);
        if (objectType != ProjectObjectType.File &&
            !string.Equals(normalizedSubtype, MermaidObjectSubtype, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw ProjectStructureAgentException.CreateAgentVisible(
            400,
            ManagedAssetCreationRequiredErrorCode,
            "File and Mermaid assets must be created with project_structure_asset_create so their content is stored as a managed asset.",
            canRetryWithCorrectedInput: true);
    }
}
