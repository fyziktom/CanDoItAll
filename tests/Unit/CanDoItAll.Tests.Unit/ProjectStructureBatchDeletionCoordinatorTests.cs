using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

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
        harness.EnqueueDeleteResult(parent.Id, new ProjectStructureDeletionResult(2, []));
        harness.EnqueueDeleteResult(sibling.Id, new ProjectStructureDeletionResult(1, []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var result = await coordinator.DeleteNodesAsync(
            projectId,
            [" child ", "parent", "parent", "sibling", " "]);

        Assert.Equal(3, result.DeletedNodeCount);
        Assert.Empty(result.DeletionWarnings);
        Assert.Equal(
            ["read", "read", "delete:parent", "delete:sibling"],
            harness.Events);
        Assert.Equal(["parent", "sibling"], harness.DeletedRootIds);
        Assert.All(
            harness.DeletedDispositions,
            disposition => Assert.Equal(
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
                disposition));
        Assert.Empty(harness.ReplayDispositions);
    }

    [Fact]
    public async Task Delete_nodes_propagates_retain_files_disposition_to_every_independent_root()
    {
        var projectId = Guid.NewGuid();
        var first = CreateNode("first");
        var second = CreateNode("second");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(first, second);
        harness.EnqueueSurface(first, second);
        harness.EnqueueDeleteResult(first.Id, new ProjectStructureDeletionResult(1, []));
        harness.EnqueueDeleteResult(second.Id, new ProjectStructureDeletionResult(1, []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var result = await coordinator.DeleteNodesAsync(
            projectId,
            [first.Id, second.Id],
            ProjectStructureManagedStorageDisposition.RetainManagedFiles);

        Assert.Equal(2, result.DeletedNodeCount);
        Assert.Equal(
            [
                ProjectStructureManagedStorageDisposition.RetainManagedFiles,
                ProjectStructureManagedStorageDisposition.RetainManagedFiles
            ],
            harness.DeletedDispositions);
        Assert.Empty(harness.ReplayDispositions);
    }

    [Fact]
    public async Task Delete_nodes_reports_completed_roots_when_a_later_storage_binding_is_invalid()
    {
        var projectId = Guid.NewGuid();
        var completedRoot = CreateNode("completed-root");
        var invalidRoot = CreateNode("invalid-root");
        var bindingId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(completedRoot, invalidRoot);
        harness.EnqueueSurface(completedRoot, invalidRoot);
        harness.EnqueueDeleteResult(completedRoot.Id, new ProjectStructureDeletionResult(2, []));
        harness.EnqueueDeleteFailure(
            invalidRoot.Id,
            new ProjectManagedStorageBindingException(bindingId, "Retargeted storage namespace."));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(
                projectId,
                [completedRoot.Id, invalidRoot.Id],
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles));

        Assert.Equal(2, exception.Recovery.CompletedNodeCount);
        Assert.Empty(exception.Recovery.Recoveries);
        var branchFailure = Assert.Single(exception.Recovery.BranchFailures);
        Assert.Equal(invalidRoot.Id, branchFailure.RootNodeId);
        Assert.Equal(
            ProjectStructureDeletionBranchFailureKind.ManagedStorageValidation,
            branchFailure.Kind);
        Assert.Equal(bindingId, branchFailure.BindingId);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            branchFailure.RequestedDisposition);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.RetainManagedFiles,
            branchFailure.SuggestedRetryDisposition);
        Assert.DoesNotContain("RetainManagedFiles", branchFailure.Remediation, StringComparison.Ordinal);
        Assert.Equal(
            [
                "read",
                "read",
                "delete:completed-root",
                "delete:invalid-root"
            ],
            harness.Events);
    }

    [Fact]
    public async Task Delete_nodes_continues_after_a_storage_binding_failure_and_reports_later_success()
    {
        var projectId = Guid.NewGuid();
        var invalidRoot = CreateNode("invalid-root");
        var completedRoot = CreateNode("completed-root");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(invalidRoot, completedRoot);
        harness.EnqueueSurface(invalidRoot, completedRoot);
        harness.EnqueueDeleteFailure(
            invalidRoot.Id,
            new ProjectManagedStorageBindingException(Guid.NewGuid(), "Retargeted storage namespace."));
        harness.EnqueueDeleteResult(completedRoot.Id, new ProjectStructureDeletionResult(1, []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(
                projectId,
                [invalidRoot.Id, completedRoot.Id],
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles));

        Assert.Equal(1, exception.Recovery.CompletedNodeCount);
        Assert.Single(exception.Recovery.BranchFailures);
        Assert.Equal(
            [
                "read",
                "read",
                "delete:invalid-root",
                "delete:completed-root"
            ],
            harness.Events);
    }

    [Fact]
    public async Task Delete_nodes_preserves_completed_evidence_when_a_later_unexpected_failure_stops_the_batch()
    {
        var projectId = Guid.NewGuid();
        var completedRoot = CreateNode("completed-root");
        var failedRoot = CreateNode("failed-root");
        var operationFailure = new InvalidOperationException("Synthetic operation failure.");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(completedRoot, failedRoot);
        harness.EnqueueSurface(completedRoot, failedRoot);
        harness.EnqueueDeleteResult(completedRoot.Id, new ProjectStructureDeletionResult(1, []));
        harness.EnqueueDeleteFailure(failedRoot.Id, operationFailure);
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(projectId, [completedRoot.Id, failedRoot.Id]));

        Assert.Equal(1, exception.Recovery.CompletedNodeCount);
        Assert.Same(operationFailure, exception.InnerException);
        var branchFailure = Assert.Single(exception.Recovery.BranchFailures);
        Assert.Equal(failedRoot.Id, branchFailure.RootNodeId);
        Assert.Equal(
            ProjectStructureDeletionBranchFailureKind.OperationFailed,
            branchFailure.Kind);
        Assert.DoesNotContain("Synthetic", branchFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_nodes_rejects_unspecified_disposition_before_accessing_storage()
    {
        var projectId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            coordinator.DeleteNodesAsync(
                projectId,
                ["node"],
                ProjectStructureManagedStorageDisposition.Unspecified));

        Assert.Empty(harness.Events);
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
    public async Task Delete_nodes_reports_committed_count_and_original_disposition_for_missing_branch_mismatch()
    {
        var projectId = Guid.NewGuid();
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface();
        harness.EnqueueSurface();
        harness.EnqueueReplayFailure(
            "committed-root",
            new ProjectStructureDeletionDispositionMismatchException(
                projectId,
                "committed-root",
                Guid.NewGuid(),
                ProjectStructureManagedStorageDisposition.RetainManagedFiles,
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
                completedNodeCount: 2));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureDeletionBatchPartialCommitException>(() =>
            coordinator.DeleteNodesAsync(
                projectId,
                ["committed-root"],
                ProjectStructureManagedStorageDisposition.RetainManagedFiles));

        Assert.Equal(2, exception.Recovery.CompletedNodeCount);
        var branchFailure = Assert.Single(exception.Recovery.BranchFailures);
        Assert.Equal(2, branchFailure.CompletedNodeCount);
        Assert.Equal(
            ProjectStructureDeletionBranchFailureKind.DispositionMismatch,
            branchFailure.Kind);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            branchFailure.SuggestedRetryDisposition);
        Assert.DoesNotContain("before deletion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_nodes_counts_every_replayless_projected_descendant()
    {
        var projectId = Guid.NewGuid();
        var projectedRoot = CreateNode("projected-root");
        var harness = new OperationHarness(projectId);
        harness.EnqueueSurface(projectedRoot);
        harness.EnqueueSurface(projectedRoot);
        harness.EnqueueDeleteResult(
            projectedRoot.Id,
            new ProjectStructureDeletionResult(3, []));
        var coordinator = new ProjectStructureBatchDeletionCoordinator(harness.CreateOperations());

        var result = await coordinator.DeleteNodesAsync(projectId, [projectedRoot.Id]);

        Assert.Equal(3, result.DeletedNodeCount);
        Assert.Equal(
            ["read", "read", "delete:projected-root"],
            harness.Events);
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
        harness.EnqueueDeleteResult(successfulRoot.Id, new ProjectStructureDeletionResult(2, []));
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
                "delete:successful-root"
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
            RetryGuidance: "Retry the exact durable mutation.",
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles);
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
        private readonly Dictionary<string, ProjectStructureDeletionResult> deleteResults = new(StringComparer.Ordinal);

        public OperationHarness(Guid projectId)
        {
            this.projectId = projectId;
        }

        public List<string> Events { get; } = [];

        public List<string> DeletedRootIds { get; } = [];

        public List<ProjectStructureManagedStorageDisposition> DeletedDispositions { get; } = [];

        public List<ProjectStructureManagedStorageDisposition> ReplayDispositions { get; } = [];

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

        public void EnqueueDeleteResult(
            string rootNodeId,
            ProjectStructureDeletionResult result)
        {
            deleteResults.Add(rootNodeId, result);
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
            ProjectStructureManagedStorageDisposition managedStorageDisposition,
            CancellationToken cancellationToken)
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add($"replay:{rootNodeId}");
            ReplayDispositions.Add(managedStorageDisposition);
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
            ProjectStructureManagedStorageDisposition managedStorageDisposition,
            CancellationToken cancellationToken)
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add($"delete:{rootNodeId}");
            DeletedRootIds.Add(rootNodeId);
            DeletedDispositions.Add(managedStorageDisposition);
            return deleteFailures.TryGetValue(rootNodeId, out var failure)
                ? Task.FromException<ProjectStructureDeletionResult>(failure)
                : Task.FromResult(deleteResults.GetValueOrDefault(
                    rootNodeId,
                    new ProjectStructureDeletionResult(0, [])));
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
