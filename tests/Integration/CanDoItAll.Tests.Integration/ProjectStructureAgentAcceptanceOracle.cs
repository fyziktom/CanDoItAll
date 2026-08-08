using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration;

internal static class Sb01ProjectStructureInvariantIds
{
    public const string ReceiptAndCanonicalEvidenceRequired = "receipt-and-canonical-evidence";
    public const string RequiredToolManifestMustBePresent = "required-tool-manifest";
    public const string CanonicalSentinelMustRemainUnchanged = "canonical-sentinel-unchanged";
    public const string CanonicalAllowedDeltaMustBeExact = "canonical-delta-exact";
}

internal enum ProjectStructureAcceptanceReceiptOutcome
{
    Succeeded,
    Failed
}

internal enum ProjectStructureAcceptanceFailure
{
    RunNotCompleted,
    RequiredToolMissingFromManifest,
    RequiredSuccessfulReceiptMissing,
    ExpectedCanonicalNodeMissing,
    ExpectedCanonicalNodeMismatch,
    CanonicalSentinelMissing,
    CanonicalSentinelDrifted,
    CanonicalBaselineNodeMissing,
    CanonicalBaselineNodeDrifted,
    ExpectedDeletedNodeStillPresent,
    UnexpectedCanonicalNode,
    DuplicateCanonicalNode,
    ExpectedCanonicalLinkMissing,
    CanonicalBaselineLinkMissing,
    ExpectedDeletedLinkStillPresent,
    UnexpectedCanonicalLink,
    DuplicateCanonicalLink,
    ExpectedCanonicalAssetMissing,
    ExpectedCanonicalAssetMismatch,
    CanonicalBaselineAssetMissing,
    CanonicalBaselineAssetDrifted,
    ExpectedDeletedAssetStillPresent,
    UnexpectedCanonicalAsset,
    DuplicateCanonicalAsset,
    ExpectedCanonicalHierarchyEdgeMissing,
    CanonicalBaselineHierarchyEdgeMissing,
    ExpectedDeletedHierarchyEdgeStillPresent,
    UnexpectedCanonicalHierarchyEdge,
    DuplicateCanonicalHierarchyEdge
}

internal sealed record ProjectStructureAcceptanceToolReceipt(
    string ToolName,
    ProjectStructureAcceptanceReceiptOutcome Outcome);

internal sealed record ProjectStructureCanonicalNodeSnapshot(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string? Notes,
    string? MetadataJson,
    string ArtifactKind = "",
    Guid? ArtifactId = null,
    string ProgressMode = "",
    int ProgressPercent = 0,
    int Priority = 0,
    int EffectivePriority = 0,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    ProjectStructureProjectRole ProjectRole = ProjectStructureProjectRole.None,
    Guid? RelatedProjectId = null,
    int ParentProjectCount = 0,
    double? X = null,
    double? Y = null,
    int? DurationSeconds = null);

internal sealed record ProjectStructureCanonicalLinkSnapshot(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored);

internal sealed record ProjectStructureCanonicalManagedAssetSnapshot(
    string NodeId,
    string MediaRelativePath,
    string? MediaContentType,
    string? MediaOriginalFileName,
    long ContentLength,
    string Sha256);

internal readonly record struct ProjectStructureCanonicalHierarchyEdgeSnapshot(
    Guid ParentProjectId,
    Guid ChildProjectId);

internal sealed record ProjectStructureCanonicalGraphSnapshot(
    IReadOnlyList<ProjectStructureCanonicalNodeSnapshot> Nodes,
    IReadOnlyList<ProjectStructureCanonicalLinkSnapshot> Links,
    IReadOnlyList<ProjectStructureCanonicalManagedAssetSnapshot> ManagedAssets,
    IReadOnlyList<ProjectStructureCanonicalHierarchyEdgeSnapshot> HierarchyEdges);

internal sealed record ProjectStructureAcceptanceContract(
    IReadOnlyList<string> RequiredToolNames,
    ProjectStructureCanonicalGraphSnapshot CanonicalGraphBefore,
    ProjectStructureCanonicalAllowedDelta AllowedDelta,
    string SentinelNodeId);

internal sealed record ProjectStructureCanonicalAllowedDelta(
    IReadOnlyList<ProjectStructureCanonicalNodeSnapshot> UpsertedNodes,
    IReadOnlyList<string> DeletedNodeIds)
{
    public IReadOnlyList<ProjectStructureCanonicalLinkSnapshot> UpsertedLinks { get; init; } = [];

    public IReadOnlyList<ProjectStructureCanonicalLinkSnapshot> DeletedLinks { get; init; } = [];

    public IReadOnlyList<ProjectStructureCanonicalManagedAssetSnapshot> UpsertedManagedAssets { get; init; } = [];

    public IReadOnlyList<string> DeletedManagedAssetNodeIds { get; init; } = [];

    public IReadOnlyList<ProjectStructureCanonicalHierarchyEdgeSnapshot> UpsertedHierarchyEdges { get; init; } = [];

    public IReadOnlyList<ProjectStructureCanonicalHierarchyEdgeSnapshot> DeletedHierarchyEdges { get; init; } = [];
}

internal sealed record ProjectStructureAcceptanceEvidence(
    ExecutionState RunState,
    string AssistantResponseText,
    IReadOnlyList<string> ToolManifest,
    IReadOnlyList<ProjectStructureAcceptanceToolReceipt> Receipts,
    ProjectStructureCanonicalGraphSnapshot CanonicalGraphAfter);

internal sealed record ProjectStructureAcceptanceRejection(
    string InvariantId,
    ProjectStructureAcceptanceFailure Failure,
    string EvidenceKey);

internal sealed record ProjectStructureAcceptanceDecision(
    IReadOnlyList<ProjectStructureAcceptanceRejection> Rejections)
{
    public bool IsAccepted => Rejections.Count == 0;
}

internal static class ProjectStructureAgentAcceptanceOracle
{
    public static ProjectStructureAcceptanceDecision Evaluate(
        ProjectStructureAcceptanceContract contract,
        ProjectStructureAcceptanceEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateContract(contract);

        var rejections = new List<ProjectStructureAcceptanceRejection>();
        if (evidence.RunState != ExecutionState.Completed)
        {
            rejections.Add(new(
                Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired,
                ProjectStructureAcceptanceFailure.RunNotCompleted,
                evidence.RunState.ToString()));
        }

        var manifest = evidence.ToolManifest.ToHashSet(StringComparer.Ordinal);
        foreach (var requiredToolName in contract.RequiredToolNames.Distinct(StringComparer.Ordinal))
        {
            if (!manifest.Contains(requiredToolName))
            {
                rejections.Add(new(
                    Sb01ProjectStructureInvariantIds.RequiredToolManifestMustBePresent,
                    ProjectStructureAcceptanceFailure.RequiredToolMissingFromManifest,
                    requiredToolName));
            }

            var hasSuccessfulReceipt = evidence.Receipts.Any(receipt =>
                string.Equals(receipt.ToolName, requiredToolName, StringComparison.Ordinal) &&
                receipt.Outcome == ProjectStructureAcceptanceReceiptOutcome.Succeeded);
            if (!hasSuccessfulReceipt)
            {
                rejections.Add(new(
                    Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired,
                    ProjectStructureAcceptanceFailure.RequiredSuccessfulReceiptMissing,
                    requiredToolName));
            }
        }

        EvaluateCanonicalNodes(contract, evidence.CanonicalGraphAfter.Nodes, rejections);
        EvaluateCanonicalLinks(contract, evidence.CanonicalGraphAfter.Links, rejections);
        EvaluateCanonicalAssets(contract, evidence.CanonicalGraphAfter.ManagedAssets, rejections);
        EvaluateCanonicalHierarchy(contract, evidence.CanonicalGraphAfter.HierarchyEdges, rejections);
        return new ProjectStructureAcceptanceDecision(rejections);
    }

    private static void EvaluateCanonicalNodes(
        ProjectStructureAcceptanceContract contract,
        IReadOnlyList<ProjectStructureCanonicalNodeSnapshot> canonicalNodesAfter,
        ICollection<ProjectStructureAcceptanceRejection> rejections)
    {
        var baselineById = contract.CanonicalGraphBefore.Nodes.ToDictionary(
            node => node.Id,
            StringComparer.Ordinal);
        var upsertsById = contract.AllowedDelta.UpsertedNodes.ToDictionary(
            node => node.Id,
            StringComparer.Ordinal);
        var deletedNodeIds = contract.AllowedDelta.DeletedNodeIds.ToHashSet(
            StringComparer.Ordinal);
        var expectedAfterById = new Dictionary<string, ProjectStructureCanonicalNodeSnapshot>(
            baselineById,
            StringComparer.Ordinal);
        foreach (var deletedNodeId in deletedNodeIds)
        {
            expectedAfterById.Remove(deletedNodeId);
        }

        foreach (var upsert in upsertsById.Values)
        {
            expectedAfterById[upsert.Id] = upsert;
        }

        var actualGroups = canonicalNodesAfter
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in actualGroups.Where(item => item.Value.Length != 1))
        {
            Reject(
                rejections,
                ProjectStructureAcceptanceFailure.DuplicateCanonicalNode,
                duplicate.Key);
        }

        foreach (var expected in expectedAfterById.Values)
        {
            var isSentinel = string.Equals(
                expected.Id,
                contract.SentinelNodeId,
                StringComparison.Ordinal);
            var isUpsert = upsertsById.ContainsKey(expected.Id);
            var invariantId = isSentinel
                ? Sb01ProjectStructureInvariantIds.CanonicalSentinelMustRemainUnchanged
                : isUpsert
                    ? Sb01ProjectStructureInvariantIds.ReceiptAndCanonicalEvidenceRequired
                    : Sb01ProjectStructureInvariantIds.CanonicalAllowedDeltaMustBeExact;
            var missingFailure = isSentinel
                ? ProjectStructureAcceptanceFailure.CanonicalSentinelMissing
                : isUpsert
                    ? ProjectStructureAcceptanceFailure.ExpectedCanonicalNodeMissing
                    : ProjectStructureAcceptanceFailure.CanonicalBaselineNodeMissing;
            var mismatchFailure = isSentinel
                ? ProjectStructureAcceptanceFailure.CanonicalSentinelDrifted
                : isUpsert
                    ? ProjectStructureAcceptanceFailure.ExpectedCanonicalNodeMismatch
                    : ProjectStructureAcceptanceFailure.CanonicalBaselineNodeDrifted;

            if (!actualGroups.TryGetValue(expected.Id, out var matches))
            {
                rejections.Add(new(invariantId, missingFailure, expected.Id));
                continue;
            }

            if (matches.Length == 1 && matches[0] != expected)
            {
                rejections.Add(new(invariantId, mismatchFailure, expected.Id));
            }
        }

        foreach (var actualNodeId in actualGroups.Keys.Where(id => !expectedAfterById.ContainsKey(id)))
        {
            Reject(
                rejections,
                deletedNodeIds.Contains(actualNodeId)
                    ? ProjectStructureAcceptanceFailure.ExpectedDeletedNodeStillPresent
                    : ProjectStructureAcceptanceFailure.UnexpectedCanonicalNode,
                actualNodeId);
        }
    }

    private static void EvaluateCanonicalLinks(
        ProjectStructureAcceptanceContract contract,
        IReadOnlyList<ProjectStructureCanonicalLinkSnapshot> canonicalLinksAfter,
        ICollection<ProjectStructureAcceptanceRejection> rejections)
    {
        var baseline = contract.CanonicalGraphBefore.Links.ToHashSet();
        var upserts = contract.AllowedDelta.UpsertedLinks.ToHashSet();
        var deleted = contract.AllowedDelta.DeletedLinks.ToHashSet();
        var expected = new HashSet<ProjectStructureCanonicalLinkSnapshot>(baseline);
        expected.ExceptWith(deleted);
        expected.UnionWith(upserts);

        var actualGroups = canonicalLinksAfter
            .GroupBy(link => link)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var duplicate in actualGroups.Where(item => item.Value != 1))
        {
            Reject(
                rejections,
                ProjectStructureAcceptanceFailure.DuplicateCanonicalLink,
                Describe(duplicate.Key));
        }

        foreach (var missing in expected.Where(link => !actualGroups.ContainsKey(link)))
        {
            Reject(
                rejections,
                upserts.Contains(missing)
                    ? ProjectStructureAcceptanceFailure.ExpectedCanonicalLinkMissing
                    : ProjectStructureAcceptanceFailure.CanonicalBaselineLinkMissing,
                Describe(missing));
        }

        foreach (var unexpected in actualGroups.Keys.Where(link => !expected.Contains(link)))
        {
            Reject(
                rejections,
                deleted.Contains(unexpected)
                    ? ProjectStructureAcceptanceFailure.ExpectedDeletedLinkStillPresent
                    : ProjectStructureAcceptanceFailure.UnexpectedCanonicalLink,
                Describe(unexpected));
        }
    }

    private static void EvaluateCanonicalAssets(
        ProjectStructureAcceptanceContract contract,
        IReadOnlyList<ProjectStructureCanonicalManagedAssetSnapshot> canonicalAssetsAfter,
        ICollection<ProjectStructureAcceptanceRejection> rejections)
    {
        var baselineByNodeId = contract.CanonicalGraphBefore.ManagedAssets.ToDictionary(
            asset => asset.NodeId,
            StringComparer.Ordinal);
        var upsertsByNodeId = contract.AllowedDelta.UpsertedManagedAssets.ToDictionary(
            asset => asset.NodeId,
            StringComparer.Ordinal);
        var deletedNodeIds = contract.AllowedDelta.DeletedManagedAssetNodeIds.ToHashSet(
            StringComparer.Ordinal);
        var expectedByNodeId = new Dictionary<string, ProjectStructureCanonicalManagedAssetSnapshot>(
            baselineByNodeId,
            StringComparer.Ordinal);
        foreach (var deletedNodeId in deletedNodeIds)
        {
            expectedByNodeId.Remove(deletedNodeId);
        }

        foreach (var upsert in upsertsByNodeId.Values)
        {
            expectedByNodeId[upsert.NodeId] = upsert;
        }

        var actualGroups = canonicalAssetsAfter
            .GroupBy(asset => asset.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var duplicate in actualGroups.Where(item => item.Value.Length != 1))
        {
            Reject(
                rejections,
                ProjectStructureAcceptanceFailure.DuplicateCanonicalAsset,
                duplicate.Key);
        }

        foreach (var expected in expectedByNodeId.Values)
        {
            var isUpsert = upsertsByNodeId.ContainsKey(expected.NodeId);
            if (!actualGroups.TryGetValue(expected.NodeId, out var matches))
            {
                Reject(
                    rejections,
                    isUpsert
                        ? ProjectStructureAcceptanceFailure.ExpectedCanonicalAssetMissing
                        : ProjectStructureAcceptanceFailure.CanonicalBaselineAssetMissing,
                    expected.NodeId);
                continue;
            }

            if (matches.Length == 1 && matches[0] != expected)
            {
                Reject(
                    rejections,
                    isUpsert
                        ? ProjectStructureAcceptanceFailure.ExpectedCanonicalAssetMismatch
                        : ProjectStructureAcceptanceFailure.CanonicalBaselineAssetDrifted,
                    expected.NodeId);
            }
        }

        foreach (var actualNodeId in actualGroups.Keys.Where(id => !expectedByNodeId.ContainsKey(id)))
        {
            Reject(
                rejections,
                deletedNodeIds.Contains(actualNodeId)
                    ? ProjectStructureAcceptanceFailure.ExpectedDeletedAssetStillPresent
                    : ProjectStructureAcceptanceFailure.UnexpectedCanonicalAsset,
                actualNodeId);
        }
    }

    private static void EvaluateCanonicalHierarchy(
        ProjectStructureAcceptanceContract contract,
        IReadOnlyList<ProjectStructureCanonicalHierarchyEdgeSnapshot> canonicalHierarchyAfter,
        ICollection<ProjectStructureAcceptanceRejection> rejections)
    {
        var baseline = contract.CanonicalGraphBefore.HierarchyEdges.ToHashSet();
        var upserts = contract.AllowedDelta.UpsertedHierarchyEdges.ToHashSet();
        var deleted = contract.AllowedDelta.DeletedHierarchyEdges.ToHashSet();
        var expected = new HashSet<ProjectStructureCanonicalHierarchyEdgeSnapshot>(baseline);
        expected.ExceptWith(deleted);
        expected.UnionWith(upserts);

        var actualGroups = canonicalHierarchyAfter
            .GroupBy(edge => edge)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var duplicate in actualGroups.Where(item => item.Value != 1))
        {
            Reject(
                rejections,
                ProjectStructureAcceptanceFailure.DuplicateCanonicalHierarchyEdge,
                Describe(duplicate.Key));
        }

        foreach (var missing in expected.Where(edge => !actualGroups.ContainsKey(edge)))
        {
            Reject(
                rejections,
                upserts.Contains(missing)
                    ? ProjectStructureAcceptanceFailure.ExpectedCanonicalHierarchyEdgeMissing
                    : ProjectStructureAcceptanceFailure.CanonicalBaselineHierarchyEdgeMissing,
                Describe(missing));
        }

        foreach (var unexpected in actualGroups.Keys.Where(edge => !expected.Contains(edge)))
        {
            Reject(
                rejections,
                deleted.Contains(unexpected)
                    ? ProjectStructureAcceptanceFailure.ExpectedDeletedHierarchyEdgeStillPresent
                    : ProjectStructureAcceptanceFailure.UnexpectedCanonicalHierarchyEdge,
                Describe(unexpected));
        }
    }

    private static void ValidateContract(ProjectStructureAcceptanceContract contract)
    {
        if (contract.RequiredToolNames.Count == 0)
        {
            throw new ArgumentException("At least one required tool is needed for acceptance.", nameof(contract));
        }

        if (contract.RequiredToolNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Required tool names cannot be empty.", nameof(contract));
        }

        ValidateUniqueKeys(
            contract.CanonicalGraphBefore.Nodes,
            node => node.Id,
            "canonical baseline nodes",
            contract);
        ValidateUniqueKeys(
            contract.AllowedDelta.UpsertedNodes,
            node => node.Id,
            "allowed node upserts",
            contract);
        ValidateDeletedKeys(
            contract.AllowedDelta.DeletedNodeIds,
            contract.CanonicalGraphBefore.Nodes.Select(node => node.Id),
            "node",
            contract,
            StringComparer.Ordinal);

        ValidateUniqueValues(
            contract.CanonicalGraphBefore.Links,
            "canonical baseline links",
            contract);
        ValidateUniqueValues(
            contract.AllowedDelta.UpsertedLinks,
            "allowed link upserts",
            contract);
        ValidateDeletedValues(
            contract.AllowedDelta.DeletedLinks,
            contract.CanonicalGraphBefore.Links,
            "link",
            contract);

        ValidateUniqueKeys(
            contract.CanonicalGraphBefore.ManagedAssets,
            asset => asset.NodeId,
            "canonical baseline managed assets",
            contract);
        ValidateUniqueKeys(
            contract.AllowedDelta.UpsertedManagedAssets,
            asset => asset.NodeId,
            "allowed managed asset upserts",
            contract);
        ValidateDeletedKeys(
            contract.AllowedDelta.DeletedManagedAssetNodeIds,
            contract.CanonicalGraphBefore.ManagedAssets.Select(asset => asset.NodeId),
            "managed asset",
            contract,
            StringComparer.Ordinal);

        ValidateUniqueValues(
            contract.CanonicalGraphBefore.HierarchyEdges,
            "canonical baseline hierarchy edges",
            contract);
        ValidateUniqueValues(
            contract.AllowedDelta.UpsertedHierarchyEdges,
            "allowed hierarchy edge upserts",
            contract);
        ValidateDeletedValues(
            contract.AllowedDelta.DeletedHierarchyEdges,
            contract.CanonicalGraphBefore.HierarchyEdges,
            "hierarchy edge",
            contract);

        var baselineNodeIds = contract.CanonicalGraphBefore.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (!baselineNodeIds.Contains(contract.SentinelNodeId))
        {
            throw new ArgumentException(
                "The canonical sentinel must exist in the baseline snapshot.",
                nameof(contract));
        }

        if (contract.AllowedDelta.DeletedNodeIds.Contains(contract.SentinelNodeId, StringComparer.Ordinal) ||
            contract.AllowedDelta.UpsertedNodes.Any(node =>
                string.Equals(node.Id, contract.SentinelNodeId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "The canonical sentinel cannot be included in the allowed delta.",
                nameof(contract));
        }

        ValidateNoKeyOverlap(
            contract.AllowedDelta.UpsertedNodes.Select(node => node.Id),
            contract.AllowedDelta.DeletedNodeIds,
            "node",
            contract,
            StringComparer.Ordinal);
        ValidateNoValueOverlap(
            contract.AllowedDelta.UpsertedLinks,
            contract.AllowedDelta.DeletedLinks,
            "link",
            contract);
        ValidateNoKeyOverlap(
            contract.AllowedDelta.UpsertedManagedAssets.Select(asset => asset.NodeId),
            contract.AllowedDelta.DeletedManagedAssetNodeIds,
            "managed asset",
            contract,
            StringComparer.Ordinal);
        ValidateNoValueOverlap(
            contract.AllowedDelta.UpsertedHierarchyEdges,
            contract.AllowedDelta.DeletedHierarchyEdges,
            "hierarchy edge",
            contract);

        ValidateExpectedGraphReferences(contract);
    }

    private static void ValidateExpectedGraphReferences(ProjectStructureAcceptanceContract contract)
    {
        var expectedNodeIds = contract.CanonicalGraphBefore.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        expectedNodeIds.ExceptWith(contract.AllowedDelta.DeletedNodeIds);
        expectedNodeIds.UnionWith(contract.AllowedDelta.UpsertedNodes.Select(node => node.Id));

        var expectedAssetNodeIds = contract.CanonicalGraphBefore.ManagedAssets
            .Select(asset => asset.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        expectedAssetNodeIds.ExceptWith(contract.AllowedDelta.DeletedManagedAssetNodeIds);
        expectedAssetNodeIds.UnionWith(
            contract.AllowedDelta.UpsertedManagedAssets.Select(asset => asset.NodeId));
        if (expectedAssetNodeIds.Any(nodeId => !expectedNodeIds.Contains(nodeId)))
        {
            throw new ArgumentException(
                "Every expected managed asset must reference an expected canonical node.",
                nameof(contract));
        }

        var expectedLinks = contract.CanonicalGraphBefore.Links.ToHashSet();
        expectedLinks.ExceptWith(contract.AllowedDelta.DeletedLinks);
        expectedLinks.UnionWith(contract.AllowedDelta.UpsertedLinks);
        if (expectedLinks.Any(link =>
                !expectedNodeIds.Contains(link.SourceId) ||
                !expectedNodeIds.Contains(link.TargetId)))
        {
            throw new ArgumentException(
                "Every expected canonical link must reference expected canonical nodes.",
                nameof(contract));
        }
    }

    private static void ValidateUniqueKeys<TItem, TKey>(
        IReadOnlyList<TItem> items,
        Func<TItem, TKey> keySelector,
        string description,
        ProjectStructureAcceptanceContract contract,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var keys = items.Select(keySelector).ToArray();
        if (keys.Any(key => key is string text && string.IsNullOrWhiteSpace(text)) ||
            keys.Length != keys.Distinct(comparer).Count())
        {
            throw new ArgumentException(
                $"The {description} must contain non-empty unique identifiers.",
                nameof(contract));
        }
    }

    private static void ValidateUniqueValues<TItem>(
        IReadOnlyList<TItem> items,
        string description,
        ProjectStructureAcceptanceContract contract)
        where TItem : notnull
    {
        if (items.Count != items.Distinct().Count())
        {
            throw new ArgumentException(
                $"The {description} must be unique.",
                nameof(contract));
        }
    }

    private static void ValidateDeletedKeys<TKey>(
        IReadOnlyList<TKey> deletedKeys,
        IEnumerable<TKey> baselineKeys,
        string description,
        ProjectStructureAcceptanceContract contract,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        comparer ??= EqualityComparer<TKey>.Default;
        if (deletedKeys.Any(key => key is string text && string.IsNullOrWhiteSpace(text)) ||
            deletedKeys.Count != deletedKeys.Distinct(comparer).Count())
        {
            throw new ArgumentException(
                $"Allowed deleted {description} identifiers must be non-empty and unique.",
                nameof(contract));
        }

        var baseline = baselineKeys.ToHashSet(comparer);
        if (deletedKeys.Any(key => !baseline.Contains(key)))
        {
            throw new ArgumentException(
                $"Allowed deleted {description} identifiers must exist in the canonical baseline.",
                nameof(contract));
        }
    }

    private static void ValidateDeletedValues<TItem>(
        IReadOnlyList<TItem> deletedValues,
        IReadOnlyList<TItem> baselineValues,
        string description,
        ProjectStructureAcceptanceContract contract)
        where TItem : notnull
    {
        ValidateUniqueValues(deletedValues, $"allowed deleted {description}s", contract);
        var baseline = baselineValues.ToHashSet();
        if (deletedValues.Any(value => !baseline.Contains(value)))
        {
            throw new ArgumentException(
                $"Allowed deleted {description}s must exist in the canonical baseline.",
                nameof(contract));
        }
    }

    private static void ValidateNoKeyOverlap<TKey>(
        IEnumerable<TKey> upsertedKeys,
        IEnumerable<TKey> deletedKeys,
        string description,
        ProjectStructureAcceptanceContract contract,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var deleted = deletedKeys.ToHashSet(comparer);
        if (upsertedKeys.Any(deleted.Contains))
        {
            throw new ArgumentException(
                $"A {description} cannot be both upserted and deleted by the allowed delta.",
                nameof(contract));
        }
    }

    private static void ValidateNoValueOverlap<TItem>(
        IReadOnlyList<TItem> upserted,
        IReadOnlyList<TItem> deleted,
        string description,
        ProjectStructureAcceptanceContract contract)
        where TItem : notnull
    {
        var deletedSet = deleted.ToHashSet();
        if (upserted.Any(deletedSet.Contains))
        {
            throw new ArgumentException(
                $"A {description} cannot be both upserted and deleted by the allowed delta.",
                nameof(contract));
        }
    }

    private static void Reject(
        ICollection<ProjectStructureAcceptanceRejection> rejections,
        ProjectStructureAcceptanceFailure failure,
        string evidenceKey)
    {
        rejections.Add(new(
            Sb01ProjectStructureInvariantIds.CanonicalAllowedDeltaMustBeExact,
            failure,
            evidenceKey));
    }

    private static string Describe(ProjectStructureCanonicalLinkSnapshot link)
    {
        return $"{link.SourceId}->{link.TargetId}:{link.Kind}:{link.IsUserAuthored}";
    }

    private static string Describe(ProjectStructureCanonicalHierarchyEdgeSnapshot edge)
    {
        return $"{edge.ParentProjectId:D}->{edge.ChildProjectId:D}";
    }
}
