using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureSubprojectTransferCoordinatorTests
{
    [Fact]
    public async Task Move_nodes_creates_linked_child_before_transfer_and_returns_exact_transfer_evidence()
    {
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var harness = new OperationHarness(sourceProjectId, targetProjectId);
        var coordinator = new ProjectStructureSubprojectTransferCoordinator(
            harness.CreateOperations(),
            () => targetProjectId);

        var result = await coordinator.MoveNodesToNewSubprojectAsync(
            sourceProjectId,
            CreateEditor(),
            [" node-a ", "node-a", "node-b"],
            includeDescendants: false);

        Assert.Equal(sourceProjectId, result.SourceProjectId);
        Assert.Equal(targetProjectId, result.TargetProjectId);
        Assert.Same(harness.SuccessfulTransfer, result.Transfer);
        Assert.Equal(["create", "move-nodes"], harness.Events);
        Assert.Equal(["node-a", "node-b"], harness.ReceivedNodeIds);
        Assert.False(harness.ReceivedIncludeDescendants);
        Assert.True(harness.TargetExists);
    }

    [Fact]
    public async Task Empty_transfer_deletes_created_child_and_preserves_the_typed_failure()
    {
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var harness = new OperationHarness(sourceProjectId, targetProjectId)
        {
            DescendantsTransfer = new ProjectStructureSubprojectTransferResult(
                targetProjectId,
                [],
                0,
                0,
                [])
        };
        var coordinator = new ProjectStructureSubprojectTransferCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureCompensatedSubprojectTransferException>(() =>
            coordinator.MoveDescendantsToNewSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                CreateEditor(),
                "anchor"));

        Assert.Equal(targetProjectId, exception.RemovedProjectId);
        var failure = Assert.IsType<ProjectStructureTransferRejectedException>(exception.TransferFailure);
        Assert.Equal(ProjectStructureTransferRejectionReason.DescendantsUnavailable, failure.Reason);
        Assert.Equal(["create", "move-descendants", "read-target", "delete", "exists"], harness.Events);
        Assert.False(harness.TargetExists);
    }

    [Fact]
    public async Task Mismatched_transfer_target_deletes_created_child_and_reports_both_target_ids()
    {
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var actualTargetProjectId = Guid.NewGuid();
        var harness = new OperationHarness(sourceProjectId, targetProjectId)
        {
            MoveNodesTransfer = new ProjectStructureSubprojectTransferResult(
                actualTargetProjectId,
                ["node-a"],
                1,
                0,
                [])
        };
        var coordinator = new ProjectStructureSubprojectTransferCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureCompensatedSubprojectTransferException>(() =>
            coordinator.MoveNodesToNewSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                CreateEditor(),
                ["node-a"],
                includeDescendants: false));

        Assert.Equal(targetProjectId, exception.RemovedProjectId);
        var rejection = Assert.IsType<ProjectStructureTransferRejectedException>(exception.TransferFailure);
        Assert.Equal(ProjectStructureTransferRejectionReason.TargetProjectMismatch, rejection.Reason);
        Assert.Equal(sourceProjectId, rejection.SourceProjectId);
        Assert.Equal(targetProjectId, rejection.TargetProjectId);
        Assert.Equal(actualTargetProjectId, rejection.ActualTargetProjectId);
        Assert.Equal(["create", "move-nodes", "read-target", "delete", "exists"], harness.Events);
        Assert.False(harness.TargetExists);
    }

    [Fact]
    public async Task Partial_commit_bypasses_empty_child_compensation_and_is_rethrown_unchanged()
    {
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var partialCommit = new ProjectStructureTransferPartialCommitException(
            new ProjectStructureTransferRecovery(
                targetProjectId,
                Guid.NewGuid(),
                ProjectStructureTransferReconciliationStatus.Failed,
                ProjectStructureTransferCommitState.WorkbenchCommitted,
                "Retry durable reconciliation."),
            "The Workbench transfer committed, but reconciliation failed.");
        var harness = new OperationHarness(sourceProjectId, targetProjectId)
        {
            MoveNodesFailure = partialCommit,
            TargetSurface = CreateSurface(targetProjectId, CreateEditableNode())
        };
        var coordinator = new ProjectStructureSubprojectTransferCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<ProjectStructureTransferPartialCommitException>(() =>
            coordinator.MoveNodesToNewSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                CreateEditor(),
                ["node-a"],
                includeDescendants: true));

        Assert.Same(partialCommit, exception);
        Assert.Equal(["create", "move-nodes"], harness.Events);
        Assert.True(harness.TargetExists);
    }

    [Fact]
    public async Task Compensation_failure_aggregates_transfer_failure_before_cleanup_failure()
    {
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var cleanupFailure = new InvalidOperationException("Delete failed.");
        var harness = new OperationHarness(sourceProjectId, targetProjectId)
        {
            DescendantsTransfer = null,
            DeleteFailure = cleanupFailure
        };
        var coordinator = new ProjectStructureSubprojectTransferCoordinator(harness.CreateOperations());

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            coordinator.MoveDescendantsToNewSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                CreateEditor(),
                "anchor"));

        var transferFailure = Assert.IsType<ProjectStructureTransferRejectedException>(exception.InnerExceptions[0]);
        Assert.Equal(ProjectStructureTransferRejectionReason.DescendantsUnavailable, transferFailure.Reason);
        Assert.Same(cleanupFailure, exception.InnerExceptions[1]);
        Assert.Equal(["create", "move-descendants", "read-target", "delete"], harness.Events);
        Assert.True(harness.TargetExists);
    }

    private static ProjectEditorModel CreateEditor()
    {
        return new ProjectEditorModel
        {
            Name = "Extracted project",
            Description = "Extracted project description.",
            Objective = "Own the selected structure.",
            CurrentPhase = "Execution",
            Status = ProjectStatus.Active
        };
    }

    private static ProjectStructureSurface CreateSurface(
        Guid projectId,
        params ProjectStructureNode[] nodes)
    {
        return new ProjectStructureSurface(
            projectId,
            "Target project",
            nodes,
            [],
            null);
    }

    private static ProjectStructureNode CreateEditableNode()
    {
        return new ProjectStructureNode(
            "node-a",
            null,
            ProjectObjectType.Note,
            "note",
            "Moved note",
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

    private sealed class OperationHarness
    {
        private readonly Guid sourceProjectId;
        private readonly Guid targetProjectId;

        public OperationHarness(Guid sourceProjectId, Guid targetProjectId)
        {
            this.sourceProjectId = sourceProjectId;
            this.targetProjectId = targetProjectId;
            SuccessfulTransfer = new ProjectStructureSubprojectTransferResult(
                targetProjectId,
                ["node-a", "node-b"],
                2,
                1,
                []);
            DescendantsTransfer = SuccessfulTransfer;
            MoveNodesTransfer = SuccessfulTransfer;
            TargetSurface = CreateSurface(targetProjectId);
        }

        public List<string> Events { get; } = [];

        public ProjectStructureSubprojectTransferResult SuccessfulTransfer { get; }

        public ProjectStructureSubprojectTransferResult? DescendantsTransfer { get; set; }

        public ProjectStructureSubprojectTransferResult MoveNodesTransfer { get; set; }

        public Exception? MoveNodesFailure { get; set; }

        public Exception? DeleteFailure { get; set; }

        public ProjectStructureSurface TargetSurface { get; set; }

        public IReadOnlyList<string> ReceivedNodeIds { get; private set; } = [];

        public bool ReceivedIncludeDescendants { get; private set; }

        public bool TargetExists { get; private set; }

        public ProjectStructureSubprojectTransferOperations CreateOperations()
        {
            return new ProjectStructureSubprojectTransferOperations(
                CreateSubprojectAsync,
                MoveDescendantsAsync,
                MoveNodesAsync,
                GetStructureAsync,
                DeleteProjectAsync,
                ProjectExistsAsync);
        }

        private Task<Result<Guid>> CreateSubprojectAsync(
            Guid receivedSourceProjectId,
            Guid receivedTargetProjectId,
            ProjectEditorModel editor,
            CancellationToken cancellationToken)
        {
            Assert.Equal(sourceProjectId, receivedSourceProjectId);
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.Equal("Extracted project", editor.Name);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("create");
            TargetExists = true;
            return Task.FromResult(Result<Guid>.Success(targetProjectId));
        }

        private Task<ProjectStructureSubprojectTransferResult?> MoveDescendantsAsync(
            Guid receivedSourceProjectId,
            string sourceNodeId,
            Guid receivedTargetProjectId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(sourceProjectId, receivedSourceProjectId);
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.Equal("anchor", sourceNodeId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("move-descendants");
            return Task.FromResult(DescendantsTransfer);
        }

        private Task<ProjectStructureSubprojectTransferResult?> MoveNodesAsync(
            Guid receivedSourceProjectId,
            IReadOnlyCollection<string> sourceNodeIds,
            Guid receivedTargetProjectId,
            bool includeDescendants,
            CancellationToken cancellationToken)
        {
            Assert.Equal(sourceProjectId, receivedSourceProjectId);
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("move-nodes");
            ReceivedNodeIds = sourceNodeIds.ToArray();
            ReceivedIncludeDescendants = includeDescendants;
            return MoveNodesFailure is null
                ? Task.FromResult<ProjectStructureSubprojectTransferResult?>(MoveNodesTransfer)
                : Task.FromException<ProjectStructureSubprojectTransferResult?>(MoveNodesFailure);
        }

        private Task<ProjectStructureSurface> GetStructureAsync(
            Guid receivedTargetProjectId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("read-target");
            return Task.FromResult(TargetSurface);
        }

        private Task DeleteProjectAsync(Guid receivedTargetProjectId, CancellationToken cancellationToken)
        {
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("delete");
            if (DeleteFailure is not null)
            {
                return Task.FromException(DeleteFailure);
            }

            TargetExists = false;
            return Task.CompletedTask;
        }

        private Task<bool> ProjectExistsAsync(Guid receivedTargetProjectId, CancellationToken cancellationToken)
        {
            Assert.Equal(targetProjectId, receivedTargetProjectId);
            Assert.False(cancellationToken.IsCancellationRequested);
            Events.Add("exists");
            return Task.FromResult(TargetExists);
        }
    }
}
