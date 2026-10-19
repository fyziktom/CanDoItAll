using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationReceipt {
    internal ProjectCreationReceipt(ProjectWriteAdmission project, DatasetNamespaceId originalNamespace, ProjectCreationReservation? reservation, string fingerprint, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(originalNamespace);
        Namespace = originalNamespace;
        Project = project;
        Reservation = reservation;
        Fingerprint = fingerprint;
        Authorization = authorization;
    }

    public ProjectWriteAdmission Project { get; }
    public DatasetNamespaceId Namespace { get; }
    public ProjectCreationReservation? Reservation { get; }
    public string Fingerprint { get; }
    internal ProjectMutationAuthorization? Authorization { get; }
}

public interface IProjectCreationCompensationGuard {
    Task<bool> CanCompensateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default);
    Task<bool> CanCancelReservationForMutationAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This compensation guard cannot inspect an original never-created reservation.");
}

public interface IProjectPartyDeletionStateQuery {
    Task<bool> HasProjectDeletionStateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default);
    Task<bool> HasReservationCancellationStateForMutationAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This party owner cannot inspect an original never-created reservation.");
}

public sealed partial class ProjectsService {
    private sealed record ProjectSaveOutcome(Guid Id, ProjectCreationReceipt? CreationReceipt, ProjectEditorAcknowledgement Editor);

    public async Task<Result<ProjectCreationReceipt>> CreateWithReceiptAsync(Guid newProjectId, ProjectEditorModel model,
        Guid? parentProjectId = null, CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null) {
        if (newProjectId == Guid.Empty || parentProjectId == Guid.Empty || model.Id.HasValue) {
            return Result<ProjectCreationReceipt>.Failure(Error.Validation("A new project requires a nonempty reserved id and cannot edit an existing project."));
        }
        var result = await SaveWithReceiptCoreAsync(model, parentProjectId, newProjectId, cancellationToken, authorization: authorization);
        return CreationResult(result);
    }

    public async Task<Result<ProjectCreationReceipt>> CreateWithReceiptAsync(ProjectCreationReservation reservation, ProjectEditorModel model,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (model.Id.HasValue) {
            return Result<ProjectCreationReceipt>.Failure(Error.Validation("A new project cannot edit an existing project."));
        }
        var result = await SaveWithReceiptCoreAsync(model, reservation.ParentProjectId, reservation.ProjectId,
            cancellationToken, reservation, authorization);
        return CreationResult(result);
    }

    public Task<ProjectCreationCompensationOutcome> CompensateCreationAsync(ProjectCreationReceipt receipt, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(receipt);
        var owner = creationCompensationOwner
            ?? throw new InvalidOperationException("The durable creation compensation owner is not installed.");
        return owner.CompensateAsync(ProjectCreationCompensationRequest.FromReceipt(receipt), cancellationToken);
    }

    public async Task<bool> TryCompensateCreationAsync(ProjectCreationReceipt receipt, CancellationToken cancellationToken = default) {
        var outcome = await CompensateCreationAsync(receipt, cancellationToken);
        if (outcome is ProjectCreationCompensationOutcome.Declined) {
            return false;
        }
        if (outcome is ProjectCreationCompensationOutcome.Committed { CompletionIssue: null, DiagnosticsUnavailable: false } committed) {
            var projection = deletionCompletionProjection
                ?? throw new InvalidOperationException("Original compensation completion observation is not installed.");
            if (await projection.ObserveAsync(committed.Completion, cancellationToken) is ProjectDeletionCompletionProjectionResult.Completed) {
                return true;
            }
        }
        throw new ProjectCreationCompensationRequiresObservationException(outcome);
    }

    private static Result<ProjectCreationReceipt> CreationResult(Result<ProjectSaveOutcome> result) {
        if (result.IsFailure) {
            return Result<ProjectCreationReceipt>.Failure(result.Errors);
        }
        return Result<ProjectCreationReceipt>.Success(result.Value?.CreationReceipt
            ?? throw new InvalidOperationException("The owner creation completed without its original creation receipt."));
    }

}
