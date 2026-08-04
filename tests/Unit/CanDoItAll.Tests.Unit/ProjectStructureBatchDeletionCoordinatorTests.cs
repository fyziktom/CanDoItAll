using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureBatchDeletionCoordinatorTests
{
    [Fact]
    public async Task Delete_nodes_normalizes_selection_reduces_descendants_and_processes_independent_roots()
    {
        var projectId = Guid.NewGuid();
        var parent = CreateNode("parent");
        var child = CreateNode("child", parent.Id);
        var sibling = CreateNode("sibling");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(parent, child, sibling);
        harness.EnqueueSurface(parent, child, sibling);
        harness.EnqueueReplay(parent.Id, new ProjectStructureDeletionReplayResult(
            [parent.Id, child.Id],
            []));
        harness.EnqueueReplay(sibling.Id, new ProjectStructureDeletionReplayResult(
            [sibling.Id],
            []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var result = await coordinator.DeleteNodesAsync(
            projectId,
            [" child ", "parent", "parent", "sibling", " "]);

        Assert.Equal(3, result.DeletedNodeCount);
        Assert.Empty(result.DeletionWarnings);
        Assert.Equal(
            ["read", "read", "delete:parent", "replay:parent", "delete:sibling", "replay:sibling"],
            harness.Events);
        Assert.Equal(["parent", "sibling"], harness.DeletedRootIds);
    }

    [Fact]
    public async Task Delete_nodes_rejects_empty_normalized_selection_without_accessing_storage()
    {
        var projectId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchRejectedException>(() =>
            coordinator.DeleteNodesAsync(projectId, [" ", "\t"]));

        Assert.Equal(
            ProjectStructureDeletionBatchRejectionReason.SelectedNodesRequired,
            exception.Reason);
        Assert.Equal("At least one project-structure node id is required.", exception.Message);
        Assert.Empty(exception.RequestedNodeIds);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task Delete_nodes_rejects_normalized_missing_selection_when_no_recovery_exists()
    {
        var projectId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface();
        harness.EnqueueSurface();
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchRejectedException>(() =>
            coordinator.DeleteNodesAsync(projectId, [" missing ", "missing"]));

        Assert.Equal(
            ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound,
            exception.Reason);
        Assert.Equal(["missing"], exception.RequestedNodeIds);
        Assert.Equal(["read", "replay:missing", "read"], harness.Events);
        Assert.Empty(harness.DeletedRootIds);
    }

    [Fact]
    public async Task Delete_nodes_replays_missing_completed_branch_without_issuing_a_second_delete()
    {
        var projectId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface();
        harness.EnqueueSurface();
        harness.EnqueueReplay("completed-root", new ProjectStructureDeletionReplayResult(
            ["completed-root", "completed-child"],
            []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var result = await coordinator.DeleteNodesAsync(projectId, ["completed-root"]);

        Assert.Equal(2, result.DeletedNodeCount);
        Assert.Equal(["read", "replay:completed-root", "read"], harness.Events);
        Assert.Empty(harness.DeletedRootIds);
    }

    [Fact]
    public async Task Delete_nodes_reports_missing_branch_partial_recovery_before_not_found()
    {
        var projectId = Guid.NewGuid();
        var recovery = CreateRecovery(projectId, "missing-root", Guid.NewGuid());
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface();
        harness.EnqueueSurface();
        harness.EnqueueReplayFailure(
            "missing-root",
            new ProjectStructureDeletionPartialCommitException(
                recovery,
                "Cleanup remains incomplete."));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(projectId, ["missing-root"]));

        Assert.Equal(0, exception.Recovery.CompletedNodeCount);
        Assert.Same(recovery, Assert.Single(exception.Recovery.Recoveries));
        Assert.Equal(["read", "replay:missing-root", "read"], harness.Events);
        Assert.Empty(harness.DeletedRootIds);
    }

    [Fact]
    public async Task Delete_nodes_aggregates_partial_commits_and_continues_later_independent_branches()
    {
        var projectId = Guid.NewGuid();
        var durableMutationId = Guid.NewGuid();
        var recovery = CreateRecovery(projectId, "missing-root", durableMutationId);
        var partialCommit = new ProjectStructureDeletionPartialCommitException(
            recovery,
            "Cleanup remains incomplete.");
        var failedRoot = CreateNode("failed-root");
        var successfulRoot = CreateNode("successful-root");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(failedRoot, successfulRoot);
        harness.EnqueueSurface(failedRoot, successfulRoot);
        harness.EnqueueReplayFailure("missing-root", partialCommit);
        harness.EnqueueDeleteFailure(failedRoot.Id, partialCommit);
        harness.EnqueueReplay(successfulRoot.Id, new ProjectStructureDeletionReplayResult(
            [successfulRoot.Id, "successful-child"],
            []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(
                projectId,
                ["missing-root", failedRoot.Id, successfulRoot.Id]));

        Assert.Equal(projectId, exception.Recovery.ProjectId);
        Assert.Equal(2, exception.Recovery.CompletedNodeCount);
        Assert.Same(recovery, Assert.Single(exception.Recovery.Recoveries));
        Assert.Empty(exception.Recovery.Warnings);
        Assert.StartsWith("2 deleted branch cleanup operation(s) remain incomplete.", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            [
                "read",
                "replay:missing-root",
                "read",
                "delete:failed-root",
                "delete:successful-root",
                "replay:successful-root"
            ],
            harness.Events);
        Assert.Equal(["failed-root", "successful-root"], harness.DeletedRootIds);
    }

    [Fact]
    public void Agent_service_keeps_only_batch_lease_and_agent_mapping_orchestration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var serviceSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureAgentService.cs"));
        var coordinatorSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureBatchDeletionCoordinator.cs"));
        var registrationSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Services",
            "WorkbenchModuleServiceCollectionExtensions.cs"));
        var batchMethodStart = serviceSource.IndexOf(
            "public Task<ProjectStructureDeletionResult> DeleteNodesDetailedAsync",
            StringComparison.Ordinal);
        var nextMethodStart = serviceSource.IndexOf(
            "public async Task<ProjectStructureNodeSummary> CreateApprovalRequestAsync",
            batchMethodStart,
            StringComparison.Ordinal);
        var batchMethodSource = serviceSource[batchMethodStart..nextMethodStart];

        Assert.Contains("ProjectStructureBatchDeletionCoordinator batchDeletionCoordinator", serviceSource, StringComparison.Ordinal);
        Assert.Contains("batchDeletionCoordinator.DeleteNodesAsync", serviceSource, StringComparison.Ordinal);
        Assert.True(
            batchMethodSource.IndexOf("NormalizeSelection", StringComparison.Ordinal) <
            batchMethodSource.IndexOf("RunWithProjectMutationLeaseAsync", StringComparison.Ordinal));
        Assert.DoesNotContain("ResolveDeleteRootNodeIds", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateBatchDeletionPartialCommitException", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayDeletionAsync", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DeletedNodeKeys", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("independent requested branches were still processed", serviceSource, StringComparison.Ordinal);

        Assert.Contains("NormalizeNodeIds", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ResolveDeleteRootNodeIds", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("ReplayMissingBranchesAsync", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("DeleteIndependentBranchesAsync", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains("CreateBatchDeletionPartialCommitException", coordinatorSource, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddScoped<ProjectStructureBatchDeletionCoordinator>()",
            registrationSource,
            StringComparison.Ordinal);
    }

    private static ProjectStructureDeletionRecovery CreateRecovery(
        Guid projectId,
        string rootNodeId,
        Guid durableMutationId)
    {
        return new ProjectStructureDeletionRecovery(
            projectId,
            rootNodeId,
            durableMutationId,
            ProjectStructureDeletionReconciliationStatus.Failed,
            ProjectStructureDeletionCommitState.WorkbenchCommitted,
            CanRetryNow: true,
            RetryAvailableAtUtc: null,
            RetryGuidance: "Retry the exact durable mutation.");
    }

    private static ProjectStructureNode CreateNode(string id, string? parentId = null)
    {
        return new ProjectStructureNode(
            id,
            parentId,
            ProjectObjectType.Note,
            "note",
            id,
            string.Empty,
            "Active",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "#000000", "N", "Note"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class OperationHarness
    {
        private readonly Guid projectId;
        private readonly Queue<ProjectStructureSurface> surfaces = [];
        private readonly Dictionary<string, Queue<object?>> replayOutcomes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Exception> deleteFailures = new(StringComparer.Ordinal);

        public OperationHarness(Guid projectId)
        {
            this.projectId = projectId;
        }

        public List<string> Events { get; } = [];

        public List<string> DeletedRootIds { get; } = [];

        public ProjectStructureBatchDeletionOperations CreateOperations()
        {
            return new ProjectStructureBatchDeletionOperations(
                GetStructureAsync,
                ReplayDeletionAsync,
                DeleteObjectDetailedAsync);
        }

        public void EnqueueSurface(params ProjectStructureNode[] nodes)
        {
            surfaces.Enqueue(new ProjectStructureSurface(
                projectId,
                "Batch deletion test",
                nodes,
                [],
                null));
        }

        public void EnqueueReplay(string rootNodeId, ProjectStructureDeletionReplayResult replay)
        {
            GetReplayOutcomes(rootNodeId).Enqueue(replay);
        }

        public void EnqueueReplayFailure(string rootNodeId, Exception failure)
        {
            GetReplayOutcomes(rootNodeId).Enqueue(failure);
        }

        public void EnqueueDeleteFailure(string rootNodeId, Exception failure)
        {
            deleteFailures.Add(rootNodeId, failure);
        }

        private Task<ProjectStructureSurface> GetStructureAsync(
            Guid receivedProjectId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("read");
            return Task.FromResult(surfaces.Dequeue());
        }

        private Task<ProjectStructureDeletionReplayResult?> ReplayDeletionAsync(
            Guid receivedProjectId,
            string rootNodeId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add($"replay:{rootNodeId}");
            if (!replayOutcomes.TryGetValue(rootNodeId, out var outcomes) || outcomes.Count == 0)
            {
                return Task.FromResult<ProjectStructureDeletionReplayResult?>(null);
            }

            var outcome = outcomes.Dequeue();
            return outcome is Exception failure
                ? Task.FromException<ProjectStructureDeletionReplayResult?>(failure)
                : Task.FromResult((ProjectStructureDeletionReplayResult?)outcome);
        }

        private Task<ProjectStructureDeletionResult> DeleteObjectDetailedAsync(
            Guid receivedProjectId,
            string rootNodeId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add($"delete:{rootNodeId}");
            DeletedRootIds.Add(rootNodeId);
            return deleteFailures.TryGetValue(rootNodeId, out var failure)
                ? Task.FromException<ProjectStructureDeletionResult>(failure)
                : Task.FromResult(new ProjectStructureDeletionResult(0, []));
        }

        private Queue<object?> GetReplayOutcomes(string rootNodeId)
        {
            if (replayOutcomes.TryGetValue(rootNodeId, out var outcomes))
            {
                return outcomes;
            }

            outcomes = [];
            replayOutcomes.Add(rootNodeId, outcomes);
            return outcomes;
        }
    }
}
