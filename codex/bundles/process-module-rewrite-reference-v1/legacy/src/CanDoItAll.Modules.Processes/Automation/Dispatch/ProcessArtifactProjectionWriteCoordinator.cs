using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionWriteRequest(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ProjectId,
    ProcessArtifactProjectionPlan ProjectionPlan,
    string FileName,
    string ContentType,
    byte[] Content,
    StorageContentKind ContentKind,
    string RelativePathHint);

internal sealed record ProcessArtifactProjectionWriteResult(
    string ManagedStoragePath,
    Guid ArtifactRecordId,
    string ExternalReferenceKey,
    Guid? ArtifactExpectationId);

internal sealed record ProcessArtifactProjectionRecordOnlyRequest(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title,
    ProcessArtifactTrustStatus TrustStatus,
    ProcessSensitivityLevel SensitivityLevel,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    string ReviewSummary,
    string ExternalReferenceKey,
    ProcessArtifactProjectionLineage ProjectionLineage);

internal sealed record ProcessArtifactProjectionRecordOnlyResult(
    Guid ArtifactRecordId,
    string ExternalReferenceKey,
    Guid? ArtifactExpectationId);

internal sealed class ProcessArtifactProjectionWriteCoordinator(
    IStoragePlacementService storagePlacementService,
    Func<ProcessArtifactRecordRequest, CancellationToken, Task<Result<Guid>>> recordArtifactAsync)
{
    public async Task<Result<ProcessArtifactProjectionWriteResult>> WriteAsync(
        ProcessArtifactProjectionWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var placement = await storagePlacementService.PlaceAsync(
            new StoragePlacementRequest(
                request.FileName,
                request.ContentType,
                request.Content,
                StorageUsagePurpose.Evidence,
                request.ContentKind,
                ProjectId: request.ProjectId,
                RelativePathHint: request.RelativePathHint),
            cancellationToken);

        var plan = request.ProjectionPlan;
        var recordResult = await recordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = request.ProcessRunId,
                StepRunId = request.StepRunId,
                ArtifactExpectationId = plan.ArtifactExpectationId,
                ArtifactKind = plan.ArtifactKind,
                Title = plan.Title,
                TrustStatus = plan.TrustStatus,
                SensitivityLevel = plan.SensitivityLevel,
                ProvenanceSummary = plan.ProvenanceSummary,
                AllowedFutureUsageSummary = plan.AllowedFutureUsageSummary,
                ReviewSummary = plan.ReviewSummary,
                ManagedStoragePath = placement.RelativePath,
                ExternalReferenceKey = plan.ExternalReferenceKey,
                ProjectionLineage = plan.ProjectionLineage
            },
            cancellationToken);

        return recordResult.IsSuccess
            ? Result<ProcessArtifactProjectionWriteResult>.Success(new ProcessArtifactProjectionWriteResult(
                placement.RelativePath,
                recordResult.Value,
                plan.ExternalReferenceKey,
                plan.ArtifactExpectationId))
            : Result<ProcessArtifactProjectionWriteResult>.Failure(recordResult.Errors);
    }
}

internal sealed class ProcessArtifactProjectionRecordOnlyCoordinator(
    Func<ProcessArtifactRecordRequest, CancellationToken, Task<Result<Guid>>> recordArtifactAsync)
{
    public async Task<Result<ProcessArtifactProjectionRecordOnlyResult>> RecordAsync(
        ProcessArtifactProjectionRecordOnlyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recordResult = await recordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = request.ProcessRunId,
                StepRunId = request.StepRunId,
                ArtifactExpectationId = request.ArtifactExpectationId,
                ArtifactKind = request.ArtifactKind,
                Title = request.Title,
                TrustStatus = request.TrustStatus,
                SensitivityLevel = request.SensitivityLevel,
                ProvenanceSummary = request.ProvenanceSummary,
                AllowedFutureUsageSummary = request.AllowedFutureUsageSummary,
                ReviewSummary = request.ReviewSummary,
                ExternalReferenceKey = request.ExternalReferenceKey,
                ProjectionLineage = request.ProjectionLineage
            },
            cancellationToken);

        return recordResult.IsSuccess
            ? Result<ProcessArtifactProjectionRecordOnlyResult>.Success(new ProcessArtifactProjectionRecordOnlyResult(
                recordResult.Value,
                request.ExternalReferenceKey,
                request.ArtifactExpectationId))
            : Result<ProcessArtifactProjectionRecordOnlyResult>.Failure(recordResult.Errors);
    }
}
