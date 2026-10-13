using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public readonly record struct ProjectStructureContentFingerprint {
    [JsonConstructor]
    public ProjectStructureContentFingerprint(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("A structure content fingerprint must be SHA-256.", nameof(value));
        }
        Value = value.ToLowerInvariant();
    }

    public string Value { get; }
}

public sealed record ProjectStructureNodeState(ProjectWriteAdmission Project, Guid RecordId, string NodeId,
    ProjectStructureContentFingerprint Content);

public sealed record ProjectStructureLinkState(ProjectWriteAdmission Project, Guid RecordId,
    string SourceId, string TargetId, ProjectObjectLinkKind Kind);

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
