using System.Security.Cryptography;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

[Description("Opaque SHA-256 fingerprint identifying the node content observed by a canonical read.")]
public readonly record struct ProjectStructureContentFingerprint {
    [JsonConstructor]
    public ProjectStructureContentFingerprint(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("A structure content fingerprint must be SHA-256.", nameof(value));
        }
        Value = value.ToLowerInvariant();
    }

    [Description("SHA-256 fingerprint of the original node content. Return the value from a canonical read unchanged; do not calculate it on the client.")]
    public string Value { get; }
}

[Description("Original project lifetime, persisted node occurrence and content required to admit a node mutation.")]
public sealed record ProjectStructureNodeState(
    [property: Description("Project lifetime that owned the node when it was read.")] ProjectWriteAdmission Project,
    [property: Description("Identity of the persisted node occurrence, distinct from its public node key.")] Guid RecordId,
    [property: Description("Public node key from the canonical read, such as custom:<id>.")] string NodeId,
    [property: Description("Fingerprint of the exact node content observed by the original read.")] ProjectStructureContentFingerprint Content);

[Description("Original project lifetime and persisted link occurrence required to admit unlinking.")]
public sealed record ProjectStructureLinkState(
    [property: Description("Project lifetime that owned the link when it was read.")] ProjectWriteAdmission Project,
    [property: Description("Identity of the persisted link occurrence.")] Guid RecordId,
    [property: Description("Original source node key.")] string SourceId,
    [property: Description("Original target node key.")] string TargetId,
    [property: Description(ProjectStructureMutationStateDescriptions.LinkKind)] ProjectObjectLinkKind Kind);

internal static class ProjectStructureMutationStateDescriptions {
    internal const string LinkKind = "Original relationship kind. Values: 0 Contains, 1 DependsOn, 2 Uses, 3 Validates, 4 Tests, 5 Blocks, 6 DerivedFrom, 7 BelongsTo. Return the value from the canonical read unchanged.";
    internal const string Node = "Original node state from a canonical read. Return it unchanged with a mutation; missing or stale state is rejected with HTTP 409.";
    internal const string Nodes = "Original states for exactly the nodes being changed, from a canonical read. Return each state unchanged; missing or stale state is rejected with HTTP 409.";
    internal const string Link = "Original link state from a canonical read. Return it unchanged when removing the link; missing or stale state is rejected with HTTP 409.";
    internal const string Graph = "Original mutation graph from a canonical structure read with includeMutationGraph=true. Return it unchanged for graph mutations; missing or stale state is rejected with HTTP 409.";
}

internal static class ProjectStructureNodeStateGuard {
    internal static IReadOnlyList<ProjectStructureNodeState> CaptureMany(IEnumerable<ProjectStructureNode> nodes, ProjectWriteAdmission project) =>
        nodes.Select(node => Capture(node, project) ?? throw new ProjectStructureEditConflictException()).ToArray();

    internal static ProjectWriteAdmission RequireProject(IReadOnlyCollection<ProjectStructureNodeState>? expected,
        Guid projectId, IEnumerable<string> nodeIds, ProjectWriteAdmission? sourceAdmission = null) {
        var keys = nodeIds.ToHashSet(StringComparer.Ordinal);
        if (expected is not { Count: > 0 } || expected.Count != keys.Count || expected.Any(state => state is null) ||
            !keys.SetEquals(expected.Select(state => state.NodeId))) {
            throw new ProjectStructureEditConflictException();
        }
        var admission = sourceAdmission ?? expected.First().Project;
        foreach (var state in expected) {
            RequireProject(state, projectId, state.NodeId, admission);
        }
        return admission;
    }

    internal static async Task EnsureCurrentAsync(WorkbenchDbContext context,
        IReadOnlyCollection<ProjectStructureNodeState>? expected, Guid projectId, IReadOnlyCollection<string> nodeIds,
        IReadOnlyCollection<ProjectObjectRecord> nodes, CancellationToken cancellationToken) {
        RequireProject(expected, projectId, nodeIds);
        if (nodes.Count != expected!.Count) {
            throw new ProjectStructureEditConflictException();
        }
        await ProjectNodeBindingStorage.LoadAsync(context, nodes, cancellationToken);
        var currentById = nodes.ToDictionary(node => node.NodeKey, StringComparer.Ordinal);
        foreach (var state in expected) {
            EnsureCurrent(state, currentById[state.NodeId]);
        }
    }

    internal static ProjectStructureNodeState? Capture(ProjectStructureNode node, ProjectWriteAdmission? project) =>
        project is null || node.RecordId is not { } recordId
            ? null : new(project, recordId, node.Id, Fingerprint(node));

    internal static ProjectWriteAdmission RequireProject(ProjectStructureNodeState? expected, Guid projectId,
        string nodeId, ProjectWriteAdmission? sourceAdmission = null) {
        if (expected is null || expected.Project is null || expected.Project.ProjectId != projectId ||
            expected.RecordId == Guid.Empty || expected.NodeId != nodeId || string.IsNullOrWhiteSpace(expected.Content.Value) ||
            sourceAdmission is not null && sourceAdmission != expected.Project) {
            throw new ProjectStructureEditConflictException();
        }
        return expected.Project;
    }

    internal static void EnsureCurrent(ProjectStructureNodeState? expected, ProjectObjectRecord current) {
        if (expected is null) {
            return;
        }
        RequireProject(expected, current.ProjectId, current.NodeKey);
        if (expected.RecordId != current.Id || expected.Content != Fingerprint(ProjectWorkbenchNodeMapper.MapStructureNode(current))) {
            throw new ProjectStructureEditConflictException();
        }
    }

    internal static ProjectStructureContentFingerprint Fingerprint(ProjectStructureNode node) {
        var markers = node.Markers.Count > 0 ? node.Markers : ProjectNodeLegacyMetadata.ReadLegacyMarkers(node.MetadataJson);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            node.ProjectId, node.RecordId, node.Id, node.ParentId, node.ObjectType, node.ObjectSubtype,
            node.Title, node.Subtitle, node.Status, node.Notes, node.Route, node.ArtifactKind, node.ArtifactId,
            node.MediaRelativePath, node.MediaContentType, node.MediaOriginalFileName, node.StorageObjectReferenceJson,
            node.ProgressMode, node.ProgressPercent, Markers = markers, node.Priority, node.StartUtc, node.EndUtc,
            node.DurationSeconds, node.MetadataJson, node.NodeReferences, node.X, node.Y, node.IsSystemManaged
        });
        return new(Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
