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

internal sealed class ProcessArtifactProjectionWriteCoordinator(
    IStoragePlacementService storagePlacementService,
    Func<ProcessArtifactRecordRequest, CancellationToken, Task<Result<Guid>>> recordArtifactAsync)
{
    public async Task<Result<string>> WriteAsync(
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
            ? Result<string>.Success(placement.RelativePath)
            : Result<string>.Failure(recordResult.Errors);
    }
}
