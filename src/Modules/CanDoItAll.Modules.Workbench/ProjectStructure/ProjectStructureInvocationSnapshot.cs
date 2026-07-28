using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

[Flags]
public enum ProjectStructureInvocationSnapshotFieldProfile
{
    None = 0,
    Identity = 1 << 0,
    Hierarchy = 1 << 1,
    Classification = 1 << 2,
    Status = 1 << 3,
    Progress = 1 << 4,
    Priority = 1 << 5,
    Schedule = 1 << 6,
    ProjectRelationship = 1 << 7,
    Links = 1 << 8,
    Selection = 1 << 9
}

public enum ProjectStructureInvocationSnapshotOmission
{
    Notes,
    Metadata,
    Assets,
    Layout,
    Routes,
    ActionCapabilities,
    StorageReferences,
    FileContents
}

public sealed class ProjectStructureInvocationSnapshotCoverage
{
    public ProjectStructureInvocationSnapshotCoverage(
        ProjectStructureInvocationSnapshotFieldProfile fieldProfile,
        IReadOnlyList<ProjectStructureInvocationSnapshotOmission> omissions,
        bool hasCompleteHierarchy,
        bool hasCompleteLinks,
        bool hasCompleteSelection,
        bool hasCompletePriorityDerivation,
        int sourceNodeCount,
        int capturedNodeCount,
        int sourceLinkCount,
        int capturedLinkCount)
    {
        if (sourceNodeCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceNodeCount));
        }

        if (capturedNodeCount < 0 || capturedNodeCount > sourceNodeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedNodeCount));
        }

        if (sourceLinkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceLinkCount));
        }

        if (capturedLinkCount < 0 || capturedLinkCount > sourceLinkCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedLinkCount));
        }

        FieldProfile = fieldProfile;
        Omissions = omissions?.Distinct().Order().ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(omissions));
        HasCompleteHierarchy = hasCompleteHierarchy;
        HasCompleteLinks = hasCompleteLinks;
        HasCompleteSelection = hasCompleteSelection;
        HasCompletePriorityDerivation = hasCompletePriorityDerivation;
        SourceNodeCount = sourceNodeCount;
        CapturedNodeCount = capturedNodeCount;
        SourceLinkCount = sourceLinkCount;
        CapturedLinkCount = capturedLinkCount;
    }

    public ProjectStructureInvocationSnapshotFieldProfile FieldProfile { get; }

    public ImmutableArray<ProjectStructureInvocationSnapshotOmission> Omissions { get; }

    public bool HasCompleteHierarchy { get; }

    public bool HasCompleteLinks { get; }

    public bool HasCompleteSelection { get; }

    public bool HasCompletePriorityDerivation { get; }

    public int SourceNodeCount { get; }

    public int CapturedNodeCount { get; }

    public int SourceLinkCount { get; }

    public int CapturedLinkCount { get; }
}

public readonly record struct ProjectStructureInvocationSnapshotNode(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Status,
    string ArtifactKind,
    Guid? ArtifactId,
    string ProgressMode,
    int ProgressPercent,
    int Priority,
    int EffectivePriority,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    ProjectStructureProjectRole ProjectRole,
    Guid? RelatedProjectId,
    int ParentProjectCount,
    int? DurationSeconds,
    bool IsFinished);

public readonly record struct ProjectStructureInvocationSnapshotLink(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored);

public sealed class ProjectStructureInvocationSnapshot : IAgentChatContextAttachment
{
    public ProjectStructureInvocationSnapshot(
        Guid projectId,
        string projectName,
        ProjectStructureAgentChatView activeView,
        IReadOnlyList<ProjectStructureInvocationSnapshotNode> nodes,
        IReadOnlyList<ProjectStructureInvocationSnapshotLink> links,
        IReadOnlyList<string> selectedNodeIds,
        ProjectStructureInvocationSnapshotCoverage coverage)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        if (!Enum.IsDefined(activeView))
        {
            throw new ArgumentOutOfRangeException(nameof(activeView));
        }

        ProjectId = projectId;
        ProjectName = projectName.Trim();
        ActiveView = activeView;
        Nodes = nodes?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(nodes));
        Links = links?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(links));
        SelectedNodeIds = selectedNodeIds?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(selectedNodeIds));
        Coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));
    }

    public Guid ProjectId { get; }

    public string ProjectName { get; }

    public ProjectStructureAgentChatView ActiveView { get; }

    public ImmutableArray<ProjectStructureInvocationSnapshotNode> Nodes { get; }

    public ImmutableArray<ProjectStructureInvocationSnapshotLink> Links { get; }

    public ImmutableArray<string> SelectedNodeIds { get; }

    public ProjectStructureInvocationSnapshotCoverage Coverage { get; }
}

internal sealed record ProjectStructureInvocationSnapshotCapture(
    ProjectStructureInvocationSnapshot Snapshot,
    AgentChatContextAttachmentDraft AttachmentDraft);

internal static partial class ProjectStructureInvocationSnapshotMapper
{
    public const string AttachmentKindValue = "project-structure.invocation-snapshot";
    public const int MaximumCapturedNodeCount = 512;
    public const int MaximumCapturedLinkCount = 1024;
    public static readonly TimeSpan FreshnessLifetime = TimeSpan.FromMinutes(5);

    private const int MaximumAnalyzedNodeCount = 4096;
    private const int MaximumIdentifierLength = 256;
    private const int MaximumProjectNameLength = 200;
    private const int MaximumTitleLength = 240;
    private const int MaximumClassificationLength = 96;
    private const int MaximumStatusLength = 96;
    private const int MaximumProgressModeLength = 48;
    private const string ContentFingerprintVersion = "project-structure-content-v1";
    private const string CoverageFingerprintVersion = "project-structure-coverage-v1";
    private const string FreshnessFingerprintVersion = "project-structure-freshness-v1";

    private static readonly ImmutableArray<ProjectStructureInvocationSnapshotOmission> Omissions =
    [
        ProjectStructureInvocationSnapshotOmission.Notes,
        ProjectStructureInvocationSnapshotOmission.Metadata,
        ProjectStructureInvocationSnapshotOmission.Assets,
        ProjectStructureInvocationSnapshotOmission.Layout,
        ProjectStructureInvocationSnapshotOmission.Routes,
        ProjectStructureInvocationSnapshotOmission.ActionCapabilities,
        ProjectStructureInvocationSnapshotOmission.StorageReferences,
        ProjectStructureInvocationSnapshotOmission.FileContents
    ];

    private const ProjectStructureInvocationSnapshotFieldProfile FieldProfile =
        ProjectStructureInvocationSnapshotFieldProfile.Identity |
        ProjectStructureInvocationSnapshotFieldProfile.Hierarchy |
        ProjectStructureInvocationSnapshotFieldProfile.Classification |
        ProjectStructureInvocationSnapshotFieldProfile.Status |
        ProjectStructureInvocationSnapshotFieldProfile.Progress |
        ProjectStructureInvocationSnapshotFieldProfile.Priority |
        ProjectStructureInvocationSnapshotFieldProfile.Schedule |
        ProjectStructureInvocationSnapshotFieldProfile.ProjectRelationship |
        ProjectStructureInvocationSnapshotFieldProfile.Links |
        ProjectStructureInvocationSnapshotFieldProfile.Selection;

    public static ProjectStructureInvocationSnapshotCapture Capture(
        ProjectStructureSurface surface,
        ProjectStructureAgentChatView activeView,
        IReadOnlyList<AgentChatContextEntityReference> selectedEntities,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selectedEntities);
        if (surface.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("The project-structure surface must identify a project.", nameof(surface));
        }

        if (!Enum.IsDefined(activeView))
        {
            throw new ArgumentOutOfRangeException(nameof(activeView));
        }

        var selectedNodeIds = selectedEntities
            .Where(static entity =>
                entity is not null &&
                string.Equals(entity.Kind, "project-node", StringComparison.Ordinal))
            .Select(static entity => entity.Id)
            .Select(NormalizeIdentifier)
            .Where(static identifier => identifier is not null)
            .Select(static identifier => identifier!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(AgentChatPositionLimits.MaximumSelectedEntities)
            .ToImmutableArray();
        var selectedNodeIdSet = selectedNodeIds.ToHashSet(StringComparer.Ordinal);
        var sourceNodes = surface.Nodes
            ?? throw new ArgumentException("The project-structure surface nodes are required.", nameof(surface));
        var sourceLinks = surface.Links
            ?? throw new ArgumentException("The project-structure surface links are required.", nameof(surface));
        var candidates = new Dictionary<string, SafeNodeCandidate>(StringComparer.Ordinal);
        var allSourceNodesSafe = true;

        foreach (var node in sourceNodes
                     .Where(static node => node is not null)
                     .OrderByDescending(node => selectedNodeIdSet.Contains(node.Id))
                     .ThenBy(static node => node.Id, StringComparer.Ordinal)
                     .Take(MaximumAnalyzedNodeCount))
        {
            if (!TryMapCandidate(node, out var candidate) ||
                !candidates.TryAdd(candidate.Id, candidate))
            {
                allSourceNodesSafe = false;
            }
        }

        if (sourceNodes.Count > MaximumAnalyzedNodeCount ||
            candidates.Count != sourceNodes.Count)
        {
            allSourceNodesSafe = false;
        }

        var allParentReferencesCovered = candidates.Values.All(candidate =>
            candidate.ParentId is null || candidates.ContainsKey(candidate.ParentId));
        var effectivePriorityBuild = BuildEffectivePriorities(candidates);
        var capturedCandidates = candidates.Values
            .OrderByDescending(candidate => selectedNodeIdSet.Contains(candidate.Id))
            .ThenBy(static candidate => candidate.Id, StringComparer.Ordinal)
            .Take(MaximumCapturedNodeCount)
            .ToArray();
        var capturedNodeIds = capturedCandidates
            .Select(static candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var capturedNodes = capturedCandidates
            .Select(candidate => candidate.ToSnapshotNode(
                effectivePriorityBuild.Priorities.GetValueOrDefault(candidate.Id)))
            .OrderBy(static node => node.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        var hasCompleteHierarchy =
            allSourceNodesSafe &&
            allParentReferencesCovered &&
            effectivePriorityBuild.IsComplete &&
            capturedNodes.Length == candidates.Count;
        var hasCompletePriorityDerivation =
            allSourceNodesSafe &&
            allParentReferencesCovered &&
            effectivePriorityBuild.IsComplete;
        var hasCompleteSelection = selectedNodeIds.All(capturedNodeIds.Contains);

        var mappedLinks = new HashSet<ProjectStructureInvocationSnapshotLink>();
        var allSourceLinksSafe = true;
        foreach (var link in sourceLinks)
        {
            if (!TryMapLink(link, capturedNodeIds, out var mappedLink))
            {
                allSourceLinksSafe = false;
                continue;
            }

            mappedLinks.Add(mappedLink);
        }

        var capturedLinks = mappedLinks
            .OrderBy(static link => link.SourceId, StringComparer.Ordinal)
            .ThenBy(static link => link.TargetId, StringComparer.Ordinal)
            .ThenBy(static link => link.Kind)
            .ThenBy(static link => link.IsUserAuthored)
            .Take(MaximumCapturedLinkCount)
            .ToImmutableArray();
        var hasCompleteLinks =
            hasCompleteHierarchy &&
            allSourceLinksSafe &&
            mappedLinks.Count == sourceLinks.Count &&
            mappedLinks.Count <= MaximumCapturedLinkCount;
        var coverage = new ProjectStructureInvocationSnapshotCoverage(
            FieldProfile,
            Omissions,
            hasCompleteHierarchy,
            hasCompleteLinks,
            hasCompleteSelection,
            hasCompletePriorityDerivation,
            sourceNodes.Count,
            capturedNodes.Length,
            sourceLinks.Count,
            capturedLinks.Length);
        var snapshot = new ProjectStructureInvocationSnapshot(
            surface.ProjectId,
            NormalizeSafeText(surface.ProjectName, MaximumProjectNameLength, "Untitled project"),
            activeView,
            capturedNodes,
            capturedLinks,
            selectedNodeIds,
            coverage);
        var contentFingerprint = ComputeContentFingerprint(snapshot);
        var coverageFingerprint = ComputeCoverageFingerprint(snapshot);
        var freshnessFingerprint = ComputeFreshnessFingerprint(
            contentFingerprint,
            coverageFingerprint,
            databaseProfileGeneration);
        var normalizedCapturedAtUtc = capturedAtUtc.ToUniversalTime();
        var attachmentDraft = new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind(AttachmentKindValue),
            contentFingerprint,
            coverageFingerprint,
            databaseProfileGeneration,
            freshnessFingerprint,
            normalizedCapturedAtUtc,
            normalizedCapturedAtUtc.Add(FreshnessLifetime),
            snapshot);

        return new ProjectStructureInvocationSnapshotCapture(snapshot, attachmentDraft);
    }

    public static ProjectStructureInvocationSnapshotCapture ReuseCurrent(
        ProjectStructureInvocationSnapshotCapture? previous,
        ProjectStructureInvocationSnapshotCapture candidate,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (previous is null ||
            previous.AttachmentDraft.FreshUntilUtc is not { } freshUntilUtc ||
            nowUtc >= freshUntilUtc)
        {
            return candidate;
        }

        return previous.AttachmentDraft.ContentFingerprint ==
               candidate.AttachmentDraft.ContentFingerprint &&
               previous.AttachmentDraft.CoverageFingerprint ==
               candidate.AttachmentDraft.CoverageFingerprint &&
               previous.AttachmentDraft.DatabaseProfileGeneration ==
               candidate.AttachmentDraft.DatabaseProfileGeneration &&
               previous.AttachmentDraft.FreshnessFingerprint ==
               candidate.AttachmentDraft.FreshnessFingerprint
            ? previous
            : candidate;
    }

    public static SnapshotContentFingerprint ComputeContentFingerprint(
        ProjectStructureInvocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var builder = new StringBuilder();
        AppendValue(builder, ContentFingerprintVersion);
        AppendValue(builder, snapshot.ProjectId.ToString("D"));
        AppendValue(builder, snapshot.ProjectName);
        AppendValue(builder, ((int)snapshot.ActiveView).ToString(CultureInfo.InvariantCulture));
        foreach (var selectedNodeId in snapshot.SelectedNodeIds.Order(StringComparer.Ordinal))
        {
            AppendValue(builder, selectedNodeId);
        }

        foreach (var node in snapshot.Nodes.OrderBy(static node => node.Id, StringComparer.Ordinal))
        {
            AppendValue(builder, node.Id);
            AppendValue(builder, node.ParentId);
            AppendValue(builder, ((int)node.ObjectType).ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.ObjectSubtype);
            AppendValue(builder, node.Title);
            AppendValue(builder, node.Status);
            AppendValue(builder, node.ArtifactKind);
            AppendValue(builder, node.ArtifactId?.ToString("D"));
            AppendValue(builder, node.ProgressMode);
            AppendValue(builder, node.ProgressPercent.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.Priority.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.EffectivePriority.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.StartUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            AppendValue(builder, node.EndUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            AppendValue(builder, ((int)node.ProjectRole).ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.RelatedProjectId?.ToString("D"));
            AppendValue(builder, node.ParentProjectCount.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.DurationSeconds?.ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, node.IsFinished ? "1" : "0");
        }

        foreach (var link in snapshot.Links
                     .OrderBy(static link => link.SourceId, StringComparer.Ordinal)
                     .ThenBy(static link => link.TargetId, StringComparer.Ordinal)
                     .ThenBy(static link => link.Kind)
                     .ThenBy(static link => link.IsUserAuthored))
        {
            AppendValue(builder, link.SourceId);
            AppendValue(builder, link.TargetId);
            AppendValue(builder, ((int)link.Kind).ToString(CultureInfo.InvariantCulture));
            AppendValue(builder, link.IsUserAuthored ? "1" : "0");
        }

        return new SnapshotContentFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    public static SnapshotCoverageFingerprint ComputeCoverageFingerprint(
        ProjectStructureInvocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var coverage = snapshot.Coverage;
        var builder = new StringBuilder();
        AppendValue(builder, CoverageFingerprintVersion);
        AppendValue(builder, ((int)coverage.FieldProfile).ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.HasCompleteHierarchy ? "1" : "0");
        AppendValue(builder, coverage.HasCompleteLinks ? "1" : "0");
        AppendValue(builder, coverage.HasCompleteSelection ? "1" : "0");
        AppendValue(builder, coverage.HasCompletePriorityDerivation ? "1" : "0");
        AppendValue(builder, coverage.SourceNodeCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.CapturedNodeCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.SourceLinkCount.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, coverage.CapturedLinkCount.ToString(CultureInfo.InvariantCulture));
        foreach (var omission in coverage.Omissions.Order())
        {
            AppendValue(builder, ((int)omission).ToString(CultureInfo.InvariantCulture));
        }

        return new SnapshotCoverageFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    public static SnapshotFreshnessFingerprint ComputeFreshnessFingerprint(
        SnapshotContentFingerprint contentFingerprint,
        SnapshotCoverageFingerprint coverageFingerprint,
        DatabaseProfileGeneration databaseProfileGeneration)
    {
        var builder = new StringBuilder();
        AppendValue(builder, FreshnessFingerprintVersion);
        AppendValue(builder, contentFingerprint.Value);
        AppendValue(builder, coverageFingerprint.Value);
        AppendValue(
            builder,
            databaseProfileGeneration.Value.ToString(CultureInfo.InvariantCulture));
        return new SnapshotFreshnessFingerprint(
            StableContentHash.ComputeSha256Hex(builder.ToString()));
    }

    private static bool TryMapCandidate(
        ProjectStructureNode node,
        out SafeNodeCandidate candidate)
    {
        var id = NormalizeIdentifier(node.Id);
        if (id is null || !Enum.IsDefined(node.ObjectType) || !Enum.IsDefined(node.ProjectRole))
        {
            candidate = default;
            return false;
        }

        var parentId = string.IsNullOrWhiteSpace(node.ParentId)
            ? null
            : NormalizeIdentifier(node.ParentId);
        if (!string.IsNullOrWhiteSpace(node.ParentId) && parentId is null)
        {
            candidate = default;
            return false;
        }

        candidate = new SafeNodeCandidate(
            id,
            parentId,
            node.ObjectType,
            NormalizeSafeText(node.ObjectSubtype, MaximumClassificationLength, "unknown"),
            NormalizeSafeText(node.Title, MaximumTitleLength, id),
            NormalizeSafeText(node.Status, MaximumStatusLength, "Unknown"),
            NormalizeSafeText(node.ArtifactKind, MaximumClassificationLength, string.Empty),
            node.ArtifactId,
            NormalizeSafeText(node.ProgressMode, MaximumProgressModeLength, string.Empty),
            Math.Clamp(node.ProgressPercent, 0, 100),
            Math.Clamp(node.Priority, 0, 6),
            node.StartUtc?.ToUniversalTime(),
            node.EndUtc?.ToUniversalTime(),
            node.ProjectRole,
            node.RelatedProjectId,
            Math.Max(0, node.ParentProjectCount),
            node.DurationSeconds is < 0 ? null : node.DurationSeconds,
            ProjectStructureChecklistRules.IsFinished(node),
            ProjectStructureChecklistRules.BlocksPriorityPropagation(node));
        return true;
    }

    private static bool TryMapLink(
        ProjectStructureLink link,
        IReadOnlySet<string> capturedNodeIds,
        out ProjectStructureInvocationSnapshotLink mappedLink)
    {
        var sourceId = NormalizeIdentifier(link.SourceId);
        var targetId = NormalizeIdentifier(link.TargetId);
        if (sourceId is null ||
            targetId is null ||
            !Enum.IsDefined(link.Kind) ||
            !capturedNodeIds.Contains(sourceId) ||
            !capturedNodeIds.Contains(targetId))
        {
            mappedLink = default;
            return false;
        }

        mappedLink = new ProjectStructureInvocationSnapshotLink(
            sourceId,
            targetId,
            link.Kind,
            link.IsUserAuthored);
        return true;
    }

    private static EffectivePriorityBuildResult BuildEffectivePriorities(
        IReadOnlyDictionary<string, SafeNodeCandidate> nodes)
    {
        var childIdsByParentId = nodes.Values
            .Where(static node => node.ParentId is not null)
            .GroupBy(static node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static node => node.Id).ToArray(),
                StringComparer.Ordinal);
        var priorities = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var isComplete = true;

        foreach (var nodeId in nodes.Keys.Order(StringComparer.Ordinal))
        {
            ComputeEffectivePriority(
                nodeId,
                nodes,
                childIdsByParentId,
                priorities,
                visiting,
                ref isComplete);
        }

        return new EffectivePriorityBuildResult(priorities, isComplete);
    }

    private static int ComputeEffectivePriority(
        string nodeId,
        IReadOnlyDictionary<string, SafeNodeCandidate> nodes,
        IReadOnlyDictionary<string, string[]> childIdsByParentId,
        IDictionary<string, int> priorities,
        ISet<string> visiting,
        ref bool isComplete)
    {
        if (priorities.TryGetValue(nodeId, out var cachedPriority))
        {
            return cachedPriority;
        }

        if (!nodes.TryGetValue(nodeId, out var node))
        {
            return 0;
        }

        if (!visiting.Add(nodeId))
        {
            isComplete = false;
            return node.Priority;
        }

        var effectivePriority = node.Priority;
        if (!node.BlocksPriorityPropagation &&
            childIdsByParentId.TryGetValue(nodeId, out var childIds))
        {
            foreach (var childId in childIds.Order(StringComparer.Ordinal))
            {
                var childPriority = ComputeEffectivePriority(
                    childId,
                    nodes,
                    childIdsByParentId,
                    priorities,
                    visiting,
                    ref isComplete);
                if (childPriority <= 0)
                {
                    continue;
                }

                effectivePriority = effectivePriority == 0
                    ? childPriority
                    : Math.Min(effectivePriority, childPriority);
            }
        }

        visiting.Remove(nodeId);
        priorities[nodeId] = effectivePriority;
        return effectivePriority;
    }

    internal static string? NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!string.Equals(value, normalized, StringComparison.Ordinal) ||
            normalized.Length > MaximumIdentifierLength ||
            normalized.IndexOfAny(['\r', '\n', '\0', '/', '\\']) >= 0 ||
            !string.Equals(
                WorkflowExecutorRedaction.RedactText(normalized),
                normalized,
                StringComparison.Ordinal))
        {
            return null;
        }

        return normalized;
    }

    internal static string NormalizeContextValue(
        string? value,
        int maximumLength,
        string fallback)
    {
        return NormalizeSafeText(value, maximumLength, fallback);
    }

    private static string NormalizeSafeText(
        string? value,
        int maximumLength,
        string fallback)
    {
        var normalized = string.Join(
            ' ',
            WorkflowExecutorRedaction.RedactText(value)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        normalized = PotentialPathRegex().Replace(normalized, "[PATH OMITTED]");
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized.Length <= maximumLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, maximumLength - 1), "…");
    }

    private static void AppendValue(StringBuilder builder, string? value)
    {
        var normalized = value ?? string.Empty;
        builder
            .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(normalized)
            .Append('|');
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}_])(?:[A-Za-z]:[\\/][^\s,;]+|\\\\[^\s,;]+|/(?:[^/\s]+/)+[^\s,;]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PotentialPathRegex();

    private readonly record struct SafeNodeCandidate(
        string Id,
        string? ParentId,
        ProjectObjectType ObjectType,
        string ObjectSubtype,
        string Title,
        string Status,
        string ArtifactKind,
        Guid? ArtifactId,
        string ProgressMode,
        int ProgressPercent,
        int Priority,
        DateTimeOffset? StartUtc,
        DateTimeOffset? EndUtc,
        ProjectStructureProjectRole ProjectRole,
        Guid? RelatedProjectId,
        int ParentProjectCount,
        int? DurationSeconds,
        bool IsFinished,
        bool BlocksPriorityPropagation)
    {
        public ProjectStructureInvocationSnapshotNode ToSnapshotNode(int effectivePriority)
        {
            return new ProjectStructureInvocationSnapshotNode(
                Id,
                ParentId,
                ObjectType,
                ObjectSubtype,
                Title,
                Status,
                ArtifactKind,
                ArtifactId,
                ProgressMode,
                ProgressPercent,
                Priority,
                effectivePriority,
                StartUtc,
                EndUtc,
                ProjectRole,
                RelatedProjectId,
                ParentProjectCount,
                DurationSeconds,
                IsFinished);
        }
    }

    private readonly record struct EffectivePriorityBuildResult(
        IReadOnlyDictionary<string, int> Priorities,
        bool IsComplete);
}
