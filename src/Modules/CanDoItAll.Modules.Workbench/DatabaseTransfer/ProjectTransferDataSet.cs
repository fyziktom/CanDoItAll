using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTransferRecordCounts(
    int Projects,
    int Phases,
    int Options,
    int HierarchyLinks,
    int Objects,
    int ObjectLinks,
    int ProjectionLayouts,
    int NodeBindings,
    int NodeReferences,
    int NodeLifecycleEvents,
    int CrossModuleMutations,
    int ViewStates,
    int Retirements = 0,
    int CreationReservations = 0,
    int WorkflowContributions = 0,
    int WorkflowAdmissions = 0,
    int WorkAssignmentHistory = 0,
    int ProcessAssetContributions = 0) {
    public int Total =>
        Projects +
        Phases +
        Options +
        HierarchyLinks +
        Objects +
        ObjectLinks +
        ProjectionLayouts +
        NodeBindings +
        NodeReferences +
        NodeLifecycleEvents +
        CrossModuleMutations +
        ViewStates + Retirements + CreationReservations + WorkflowContributions + WorkflowAdmissions + WorkAssignmentHistory + ProcessAssetContributions;
}

internal sealed class ProjectTransferDataSet {
    public List<ProjectTransferProject> Projects { get; set; } = [];

    public List<ProjectTransferPhase> Phases { get; set; } = [];

    public List<ProjectTransferOption> Options { get; set; } = [];

    public List<ProjectTransferHierarchy> HierarchyLinks { get; set; } = [];

    public List<ProjectObjectRecord> Objects { get; set; } = [];

    public List<ProjectObjectLinkRecord> ObjectLinks { get; set; } = [];

    public List<ProjectStructureProjectionLayoutRecord> ProjectionLayouts { get; set; } = [];

    public List<ProjectNodeBindingRecord> NodeBindings { get; set; } = [];

    public List<ProjectNodeReferenceRecord> NodeReferences { get; set; } = [];

    public List<ProjectNodeLifecycleEventRecord> NodeLifecycleEvents { get; set; } = [];

    public List<ProjectCrossModuleMutationRecord> CrossModuleMutations { get; set; } = [];

    public List<ProjectWorkbenchViewStateRecord> ViewStates { get; set; } = [];

    public List<ProjectTransferRetirement> Retirements { get; set; } = [];

    public List<ProjectTransferReservation> CreationReservations { get; set; } = [];

    public List<ProjectWorkflowContributionRecord> WorkflowContributions { get; set; } = [];

    public List<ProjectWorkflowAdmissionRecord> WorkflowAdmissions { get; set; } = [];

    public List<ProjectWorkAssignmentTransferItem> WorkAssignmentHistory { get; set; } = [];

    public List<ProjectProcessAssetContributionRecord> ProcessAssetContributions { get; set; } = [];

    public ProjectTransferRecordCounts Counts => new(
        Projects.Count,
        Phases.Count,
        Options.Count,
        HierarchyLinks.Count,
        Objects.Count,
        ObjectLinks.Count,
        ProjectionLayouts.Count,
        NodeBindings.Count,
        NodeReferences.Count,
        NodeLifecycleEvents.Count,
        CrossModuleMutations.Count,
        ViewStates.Count,
        Retirements.Count,
        CreationReservations.Count,
        WorkflowContributions.Count,
        WorkflowAdmissions.Count,
        WorkAssignmentHistory.Count,
        ProcessAssetContributions.Count);

    public void PrepareForTargetImport(Guid sourceProfileId, Guid transferId) {
        var provenance = new RetainedEvidenceImport(sourceProfileId, transferId);
        Projects = JsonSerializer.Deserialize<List<ProjectTransferProject>>(JsonSerializer.SerializeToUtf8Bytes(Projects))
            ?? throw new InvalidDataException("The project transfer payload is invalid.");
        foreach (var row in Retirements) {
            row.ImportedHistory = Imported(row.ImportedHistory);
        }
        foreach (var row in CreationReservations) {
            row.ImportedHistory = Imported(row.ImportedHistory);
        }
        foreach (var row in WorkflowContributions) {
            row.ImportedHistory = Imported(row.ImportedHistory);
        }
        foreach (var row in WorkflowAdmissions) {
            row.ImportedHistory = Imported(row.ImportedHistory);
        }

        WorkAssignmentHistory = WorkAssignmentHistory.Select(item => item with { ImportedHistory = Imported(item.ImportedHistory) }).ToList();
        foreach (var row in ProcessAssetContributions) {
            row.ImportedHistory = Imported(row.ImportedHistory);
        }

        RetainedEvidenceImport Imported(RetainedEvidenceImport? previous) => previous is null
            ? provenance : new(sourceProfileId, transferId, previous);
    }

    public bool HasStorageBindings => NodeBindings.Any(binding =>
        !string.IsNullOrWhiteSpace(binding.MediaRelativePath) ||
        !string.IsNullOrWhiteSpace(binding.StorageObjectReferenceJson));

    public bool HasCrossModuleMutations => CrossModuleMutations.Count > 0;

    public void PrepareForPackageExport() {
        if (CrossModuleMutations.Any(mutation =>
                mutation.Status != ProjectCrossModuleMutationStatus.Completed)) {
            throw new InvalidDataException(
                "Project package export cannot capture pending or failed cross-module recovery work. Complete or resolve it before exporting.");
        }

        CrossModuleMutations.Clear();
    }

    public void ValidatePackageImportSafety() {
        if (CrossModuleMutations.Count > 0) {
            throw new InvalidDataException(
                "Project package v2 cannot import executable cross-module mutation records.");
        }
    }

    public void ValidateForImport() {
        ValidateUniqueIds(Projects, item => item.Id, "project");
        ValidateUniqueIds(Phases, item => item.Id, "project phase");
        ValidateUniqueIds(Options, item => item.Id, "project option");
        ValidateUniqueIds(HierarchyLinks, item => item.Id, "project hierarchy link");
        ValidateUniqueIds(Objects, item => item.Id, "project object");
        ValidateUniqueIds(ObjectLinks, item => item.Id, "project object link");
        ValidateUniqueIds(ProjectionLayouts, item => item.Id, "project projection layout");
        ValidateUniqueIds(NodeBindings, item => item.Id, "project node binding");
        ValidateUniqueIds(NodeReferences, item => item.Id, "project node reference");
        ValidateUniqueIds(NodeLifecycleEvents, item => item.Id, "project node lifecycle event");
        ValidateUniqueIds(CrossModuleMutations, item => item.Id, "project cross-module mutation");
        ValidateUniqueIds(ViewStates, item => item.Id, "project view state");
        ValidateUniqueIds(Retirements, item => item.LifetimeId, "project retirement lifetime");
        ValidateUniqueIds(CreationReservations, item => item.Id, "project creation reservation");
        ValidateUniqueIds(CreationReservations, item => item.LifetimeId, "reserved project lifetime");
        ValidateUniqueIds(WorkflowAdmissions, item => item.IntentId, "workflow admission intent");
        ValidateUniqueIds(WorkflowAdmissions, item => item.RunId, "workflow admission run");
        ValidateUniqueIds(WorkAssignmentHistory, item => item.EvidenceId, "Work assignment history evidence");
        ValidateUniqueIds(ProcessAssetContributions, item => item.IntentId, "Process asset contribution intent");
        ValidateUniqueIds(ProcessAssetContributions, item => item.NativeObjectId, "Process asset native identity");
        ValidateUniqueIds(ProcessAssetContributions, item => item.StorageIntentId, "Process asset Storage intent");
        foreach (var item in WorkAssignmentHistory) {
            _ = ProjectWorkAssignmentHistoryRecord.Validate(item);
        }
        ValidateRetainedHistory();

        var projectIds = Projects.Select(item => item.Id).ToHashSet();
        ValidateProjectReferences(Phases, item => item.ProjectId, projectIds, "project phase");
        ValidateProjectReferences(Options, item => item.ProjectId, projectIds, "project option");
        ValidateProjectReferences(Objects, item => item.ProjectId, projectIds, "project object");
        ValidateProjectReferences(ObjectLinks, item => item.ProjectId, projectIds, "project object link");
        ValidateProjectReferences(ProjectionLayouts, item => item.ProjectId, projectIds, "project projection layout");
        ValidateProjectReferences(NodeLifecycleEvents, item => item.ProjectId, projectIds, "project node lifecycle event");
        ValidateProjectReferences(CrossModuleMutations, item => item.ProjectId, projectIds, "project cross-module mutation");
        ValidateProjectReferences(ViewStates, item => item.ProjectId, projectIds, "project view state");

        var hierarchyEdges = new HashSet<(Guid ParentId, Guid ChildId)>();
        foreach (var hierarchyLink in HierarchyLinks) {
            if (!projectIds.Contains(hierarchyLink.ParentProjectId) ||
                !projectIds.Contains(hierarchyLink.ChildProjectId) ||
                hierarchyLink.ParentProjectId == hierarchyLink.ChildProjectId ||
                !hierarchyEdges.Add((
                    hierarchyLink.ParentProjectId,
                    hierarchyLink.ChildProjectId))) {
                throw InvalidReference("project hierarchy link", hierarchyLink.Id);
            }
        }
        ValidateProjectHierarchyIsAcyclic(projectIds);

        var objectsById = Objects.ToDictionary(item => item.Id);
        var objectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectObject in Objects) {
            if (string.IsNullOrWhiteSpace(projectObject.NodeKey) ||
                !objectKeys.Add(ToNodeIdentity(
                    projectObject.ProjectId,
                    projectObject.NodeKey))) {
                throw new InvalidDataException(
                    $"Project package object '{projectObject.Id:D}' has an empty or duplicate node key.");
            }
        }

        foreach (var projectObject in Objects.Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeKey))) {
            if (!IsCanonicalProjectRoot(projectObject.ProjectId, projectObject.ParentNodeKey!) &&
                !objectKeys.Contains(ToNodeIdentity(
                    projectObject.ProjectId,
                    projectObject.ParentNodeKey!))) {
                throw InvalidReference("project object parent", projectObject.Id);
            }
        }
        ValidateNodeHierarchyIsAcyclic();

        foreach (var objectLink in ObjectLinks) {
            if (!objectKeys.Contains(ToNodeIdentity(
                    objectLink.ProjectId,
                    objectLink.SourceNodeKey)) ||
                !objectKeys.Contains(ToNodeIdentity(
                    objectLink.ProjectId,
                    objectLink.TargetNodeKey)) ||
                string.Equals(
                    objectLink.SourceNodeKey,
                    objectLink.TargetNodeKey,
                    StringComparison.OrdinalIgnoreCase)) {
                throw InvalidReference("project object link", objectLink.Id);
            }
        }

        foreach (var layout in ProjectionLayouts) {
            if (!objectKeys.Contains(ToNodeIdentity(
                    layout.ProjectId,
                    layout.NodeKey))) {
                throw InvalidReference("project projection layout", layout.Id);
            }
        }

        ValidateObjectReferences(NodeBindings, item => item.ProjectObjectId, objectsById, "project node binding");
        ValidateObjectReferences(NodeReferences, item => item.ProjectObjectId, objectsById, "project node reference");
        ValidateObjectReferences(NodeLifecycleEvents, item => item.ProjectObjectId, objectsById, "project node lifecycle event");

        if (NodeBindings.Select(item => item.ProjectObjectId).Distinct().Count() != NodeBindings.Count) {
            throw new InvalidDataException("Project package contains duplicate node bindings for one project object.");
        }
    }

    private void ValidateRetainedHistory() {
        if (ProcessAssetContributions.Any(row => row.DatabaseProfileId == Guid.Empty || row.ProjectId == Guid.Empty ||
                row.ProjectLifetimeId == Guid.Empty || row.SourceExecutionRunId == Guid.Empty ||
                string.IsNullOrWhiteSpace(row.PlanJson) || string.IsNullOrWhiteSpace(row.PlanFingerprint) ||
                row.PlanFingerprint.Length > 64 || row.MaterializedRequestJson is null || row.MaterializedFingerprint is null ||
                row.MaterializedFingerprint.Length > 64 || row.NodeJson is null || row.ReceiptJson is null) ||
            Retirements.Any(row => row.ProjectId == Guid.Empty) ||
            CreationReservations.Any(row => row.DatabaseProfileId == Guid.Empty || row.ProjectId == Guid.Empty ||
                row.RequesterId == Guid.Empty || row.ParentProjectId.HasValue != row.ParentLifetimeId.HasValue ||
                row.ParentProjectId == Guid.Empty || row.ParentLifetimeId == Guid.Empty) ||
            WorkflowContributions.Any(row => row.RunId == Guid.Empty || row.ProjectId == Guid.Empty ||
                string.IsNullOrWhiteSpace(row.OccurrencePath) || row.Slot < 0) ||
            WorkflowAdmissions.Any(row => row.ProjectId == Guid.Empty || row.NativeNodeId == Guid.Empty ||
                string.IsNullOrWhiteSpace(row.NodeId) || row.Sequence <= 0)) {
            throw new InvalidDataException("The project package contains invalid retained history identities.");
        }
        if (WorkflowContributions.Select(row => (row.RunId, row.OccurrencePath, row.Slot)).Distinct().Count() != WorkflowContributions.Count ||
            WorkflowAdmissions.Select(row => (row.ProjectId, row.NodeId, row.Sequence)).Distinct().Count() != WorkflowAdmissions.Count) {
            throw new InvalidDataException("The project package contains duplicate retained history identities.");
        }
    }

    private static void ValidateUniqueIds<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> idSelector,
        string label) {
        var ids = new HashSet<Guid>();
        foreach (var row in rows) {
            var id = idSelector(row);
            if (id == Guid.Empty || !ids.Add(id)) {
                throw new InvalidDataException(
                    $"Project package contains an empty or duplicate {label} id.");
            }
        }
    }

    private static void ValidateProjectReferences<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> projectIdSelector,
        IReadOnlySet<Guid> projectIds,
        string label) {
        foreach (var row in rows) {
            var projectId = projectIdSelector(row);
            if (!projectIds.Contains(projectId)) {
                throw new InvalidDataException(
                    $"Project package {label} references missing project '{projectId:D}'.");
            }
        }
    }

    private static void ValidateObjectReferences<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> objectIdSelector,
        IReadOnlyDictionary<Guid, ProjectObjectRecord> objectsById,
        string label) {
        foreach (var row in rows) {
            var objectId = objectIdSelector(row);
            if (!objectsById.ContainsKey(objectId)) {
                throw new InvalidDataException(
                    $"Project package {label} references missing project object '{objectId:D}'.");
            }
        }
    }

    private static InvalidDataException InvalidReference(string label, Guid id)
        => new($"Project package {label} '{id:D}' contains a dangling reference.");

    private static string ToNodeIdentity(Guid projectId, string nodeKey)
        => $"{projectId:N}\0{nodeKey}";

    private static bool IsCanonicalProjectRoot(Guid projectId, string nodeKey)
        => string.Equals(
            nodeKey,
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            StringComparison.OrdinalIgnoreCase);

    private void ValidateProjectHierarchyIsAcyclic(IReadOnlySet<Guid> projectIds) {
        var childrenByParent = projectIds.ToDictionary(
            projectId => projectId,
            _ => new List<Guid>());
        var incomingEdges = projectIds.ToDictionary(
            projectId => projectId,
            _ => 0);
        foreach (var link in HierarchyLinks) {
            childrenByParent[link.ParentProjectId].Add(link.ChildProjectId);
            incomingEdges[link.ChildProjectId]++;
        }

        var ready = new Queue<Guid>(incomingEdges
            .Where(item => item.Value == 0)
            .Select(item => item.Key));
        var visited = 0;
        while (ready.TryDequeue(out var projectId)) {
            visited++;
            foreach (var childProjectId in childrenByParent[projectId]) {
                incomingEdges[childProjectId]--;
                if (incomingEdges[childProjectId] == 0) {
                    ready.Enqueue(childProjectId);
                }
            }
        }

        if (visited != projectIds.Count) {
            throw new InvalidDataException(
                "Project package hierarchy contains a cycle.");
        }
    }

    private void ValidateNodeHierarchyIsAcyclic() {
        foreach (var projectObjects in Objects.GroupBy(item => item.ProjectId)) {
            var parentByNode = projectObjects.ToDictionary(
                item => item.NodeKey,
                item => item.ParentNodeKey,
                StringComparer.OrdinalIgnoreCase);
            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeKey in parentByNode.Keys) {
                if (completed.Contains(nodeKey)) {
                    continue;
                }

                var currentPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var visitedPath = new List<string>();
                string? currentNodeKey = nodeKey;
                while (!string.IsNullOrWhiteSpace(currentNodeKey) &&
                       !IsCanonicalProjectRoot(projectObjects.Key, currentNodeKey) &&
                       !completed.Contains(currentNodeKey)) {
                    if (!currentPath.Add(currentNodeKey)) {
                        throw new InvalidDataException(
                            "Project package node parent graph contains a cycle.");
                    }

                    visitedPath.Add(currentNodeKey);
                    currentNodeKey = parentByNode[currentNodeKey];
                }

                completed.UnionWith(visitedPath);
            }
        }
    }
}
