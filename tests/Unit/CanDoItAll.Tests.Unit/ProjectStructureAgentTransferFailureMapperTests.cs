using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentTransferFailureMapperTests
{
    public static TheoryData<ProjectStructureDeletionBatchRejectionReason, int, string> BatchDeletionRejectionMappings => new()
    {
        { ProjectStructureDeletionBatchRejectionReason.SelectedNodesRequired, 400, "SelectedNodesRequired" },
        { ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound, 404, "SelectedNodesNotFound" }
    };

    public static TheoryData<ProjectStructureTransferRejectionReason, int, string> RejectionMappings => new()
    {
        { ProjectStructureTransferRejectionReason.SourceProjectRequired, 400, "ProjectIdRequired" },
        { ProjectStructureTransferRejectionReason.TargetProjectRequired, 400, "SubprojectIdRequired" },
        { ProjectStructureTransferRejectionReason.TargetProjectMustDiffer, 400, "TargetProjectMustDiffer" },
        { ProjectStructureTransferRejectionReason.SelectedNodesRequired, 400, "SelectedNodesRequired" },
        { ProjectStructureTransferRejectionReason.DescendantsUnavailable, 400, "DescendantsTransferUnavailable" },
        { ProjectStructureTransferRejectionReason.SelectedNodesUnavailable, 400, "SelectedNodesTransferUnavailable" },
        { ProjectStructureTransferRejectionReason.TargetProjectMismatch, 500, "SubprojectTransferTargetMismatch" }
    };

    [Fact]
    public void Deletion_disposition_mismatch_maps_to_a_correctable_safe_conflict()
    {
        var applicationFailure = new ProjectStructureDeletionDispositionMismatchException(
            Guid.NewGuid(),
            "deleted-root",
            Guid.NewGuid(),
            ProjectStructureManagedStorageDisposition.RetainManagedFiles,
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            completedNodeCount: 2);

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(409, agentFailure.StatusCode);
        Assert.Equal("ProjectStructureDeletionDispositionMismatch", agentFailure.ErrorCode);
        Assert.True(agentFailure.IsSafeToExpose);
        Assert.True(agentFailure.CanRetryWithCorrectedInput);
        var details = JsonSerializer.Serialize(agentFailure.Details);
        Assert.Contains("RetainManagedFiles", details, StringComparison.Ordinal);
        Assert.Contains("DeleteOwnedManagedFiles", details, StringComparison.Ordinal);
        Assert.Contains("deleted-root", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Contains("2 node(s)", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Contains("DeleteOwnedManagedFiles", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Same(applicationFailure, agentFailure.InnerException);

        Assert.True(MafAgentToolFailureMapper.TryMap(agentFailure, out var toolFailure));
        Assert.True(toolFailure.CanRetryWithCorrectedInput);
        Assert.Contains("deleted-root", toolFailure.Message, StringComparison.Ordinal);
        Assert.Contains("DeleteOwnedManagedFiles", toolFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Batch_storage_validation_failure_maps_truthful_completed_evidence_as_a_correctable_conflict()
    {
        var branchFailure = new ProjectStructureDeletionBranchFailure(
            "invalid-root",
            ProjectStructureDeletionBranchFailureKind.ManagedStorageValidation,
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            Guid.NewGuid(),
            "Managed-file ownership could not be verified.",
            "Retry with the node-only deletion option.")
        {
            SuggestedRetryDisposition = ProjectStructureManagedStorageDisposition.RetainManagedFiles
        };
        var recovery = new ProjectStructureDeletionBatchRecovery(
            Guid.NewGuid(),
            [],
            CompletedNodeCount: 2,
            Warnings: [])
        {
            BranchFailures = [branchFailure]
        };
        var applicationFailure = new ProjectStructureDeletionBatchPartialCommitException(
            recovery,
            "Two nodes were deleted, but one branch failed before deletion.");

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(409, agentFailure.StatusCode);
        Assert.Equal("ProjectStructureDeletionBatchPartialCommit", agentFailure.ErrorCode);
        Assert.Same(recovery, agentFailure.Details);
        Assert.True(agentFailure.IsSafeToExpose);
        Assert.True(agentFailure.CanRetryWithCorrectedInput);
        Assert.Contains("invalid-root", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Contains("ManagedStorageValidation", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Contains("RetainManagedFiles", agentFailure.SafeMessage, StringComparison.Ordinal);
        Assert.Same(applicationFailure, agentFailure.InnerException);

        Assert.True(MafAgentToolFailureMapper.TryMap(agentFailure, out var toolFailure));
        Assert.True(toolFailure.CanRetryWithCorrectedInput);
        Assert.Contains("invalid-root", toolFailure.Message, StringComparison.Ordinal);
        Assert.Contains("RetainManagedFiles", toolFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Batch_unexpected_branch_failure_does_not_claim_corrected_input_can_fix_it()
    {
        var recovery = new ProjectStructureDeletionBatchRecovery(
            Guid.NewGuid(),
            [],
            CompletedNodeCount: 1,
            Warnings: [])
        {
            BranchFailures =
            [
                new ProjectStructureDeletionBranchFailure(
                    "failed-root",
                    ProjectStructureDeletionBranchFailureKind.OperationFailed,
                    ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
                    BindingId: null,
                    "Unexpected failure.",
                    "Inspect the server log.")
            ]
        };
        var applicationFailure = new ProjectStructureDeletionBatchPartialCommitException(
            recovery,
            "One node was deleted before an unexpected failure.");

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.True(MafAgentToolFailureMapper.TryMap(agentFailure, out var toolFailure));
        Assert.False(toolFailure.CanRetryWithCorrectedInput);
        Assert.Contains("failed-root", toolFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic", toolFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Creation_rejection_maps_to_the_existing_agent_transport_contract()
    {
        IReadOnlyList<Error> errors = [new Error("DuplicateName", "A project with that name already exists.")];
        var applicationFailure = new ProjectStructureProjectCreationRejectedException(
            "The project could not be created.",
            errors);

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(400, agentFailure.StatusCode);
        Assert.Equal("ProjectCreationRejected", agentFailure.ErrorCode);
        Assert.Equal(applicationFailure.Message, agentFailure.Message);
        Assert.Same(errors, agentFailure.Details);
        Assert.False(agentFailure.IsSafeToExpose);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.Same(applicationFailure, agentFailure.InnerException);
    }

    [Fact]
    public void Existing_project_disappearing_during_save_maps_to_project_not_found()
    {
        IReadOnlyList<Error> errors =
        [
            Error.Failure(
                "The project no longer exists.",
                ProjectErrorCodes.NotFound)
        ];
        var applicationFailure = new ProjectStructureProjectCreationRejectedException(
            "The project no longer exists.",
            errors);

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(404, agentFailure.StatusCode);
        Assert.Equal("ProjectNotFound", agentFailure.ErrorCode);
        Assert.Equal(applicationFailure.Message, agentFailure.Message);
        Assert.Same(errors, agentFailure.Details);
        Assert.False(agentFailure.IsSafeToExpose);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.Same(applicationFailure, agentFailure.InnerException);
    }

    [Theory]
    [MemberData(nameof(RejectionMappings))]
    public void Transfer_rejection_maps_to_the_existing_agent_status_and_error_code(
        ProjectStructureTransferRejectionReason reason,
        int expectedStatusCode,
        string expectedErrorCode)
    {
        var applicationFailure = new ProjectStructureTransferRejectedException(
            reason,
            "Transfer rejected.",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(expectedStatusCode, agentFailure.StatusCode);
        Assert.Equal(expectedErrorCode, agentFailure.ErrorCode);
        Assert.Equal(applicationFailure.Message, agentFailure.Message);
        Assert.False(agentFailure.IsSafeToExpose);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.Same(applicationFailure, agentFailure.InnerException);
    }

    [Theory]
    [MemberData(nameof(BatchDeletionRejectionMappings))]
    public void Batch_deletion_rejection_maps_to_the_existing_agent_status_and_error_code(
        ProjectStructureDeletionBatchRejectionReason reason,
        int expectedStatusCode,
        string expectedErrorCode)
    {
        IReadOnlyList<string> requestedNodeIds = reason == ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound
            ? ["node-a"]
            : [];
        var applicationFailure = new ProjectStructureDeletionBatchRejectedException(
            reason,
            "Batch deletion rejected.",
            requestedNodeIds);

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            applicationFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(expectedStatusCode, agentFailure.StatusCode);
        Assert.Equal(expectedErrorCode, agentFailure.ErrorCode);
        Assert.Equal(applicationFailure.Message, agentFailure.Message);
        Assert.Equal(
            reason == ProjectStructureDeletionBatchRejectionReason.SelectedNodesNotFound,
            agentFailure.Details is not null);
        Assert.False(agentFailure.IsSafeToExpose);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.Same(applicationFailure, agentFailure.InnerException);
    }

    [Fact]
    public void Compensated_transfer_maps_the_application_failure_and_retains_compensation_evidence()
    {
        var transferFailure = new ProjectStructureTransferRejectedException(
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable,
            "The selected nodes could not be moved.",
            Guid.NewGuid(),
            Guid.NewGuid());
        var compensatedFailure = new ProjectStructureCompensatedSubprojectTransferException(
            transferFailure.TargetProjectId,
            transferFailure);

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            compensatedFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(400, agentFailure.StatusCode);
        Assert.Equal("SelectedNodesTransferUnavailable", agentFailure.ErrorCode);
        Assert.Same(compensatedFailure, agentFailure.InnerException);
    }

    [Fact]
    public void Nested_partial_commit_maps_recovery_evidence_as_safe_agent_details()
    {
        var recovery = new ProjectStructureTransferRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProjectStructureTransferReconciliationStatus.Failed,
            ProjectStructureTransferCommitState.WorkbenchCommitted,
            "Retry durable reconciliation.");
        var partialCommit = new ProjectStructureTransferPartialCommitException(
            recovery,
            "The transfer committed, but reconciliation failed.");
        var outerFailure = new AggregateException(
            partialCommit,
            new InvalidOperationException("Lease cleanup failed."));

        var wasMapped = ProjectStructureAgentTransferFailureMapper.TryMap(
            outerFailure,
            out var agentFailure);

        Assert.True(wasMapped);
        Assert.Equal(409, agentFailure.StatusCode);
        Assert.Equal("ProjectStructureTransferPartialCommit", agentFailure.ErrorCode);
        Assert.Same(recovery, agentFailure.Details);
        Assert.True(agentFailure.IsSafeToExpose);
        Assert.False(agentFailure.CanRetryWithCorrectedInput);
        Assert.Same(outerFailure, agentFailure.InnerException);
    }
}
