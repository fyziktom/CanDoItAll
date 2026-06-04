using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessArtifactProjectionWriteCoordinatorTests
{
    [Fact]
    public async Task WriteAsync_SB03_INV_001_returns_structured_outcome_and_records_request()
    {
        var recordId = Guid.NewGuid();
        ProcessArtifactRecordRequest? capturedRecord = null;
        var storage = new RecordingStoragePlacementService("managed/process/evidence.txt");
        var coordinator = new ProcessArtifactProjectionWriteCoordinator(
            storage,
            (request, _) => {
                capturedRecord = request;
                return Task.FromResult(Result<Guid>.Success(recordId));
            });
        var expectationId = Guid.NewGuid();
        var plan = CreatePlan(
            expectationId,
            "agentframework-artifact:source",
            "agentframework-artifact:source|projected");

        var result = await coordinator.WriteAsync(
            new ProcessArtifactProjectionWriteRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                plan,
                "evidence.txt",
                "text/plain",
                "hello"u8.ToArray(),
                StorageContentKind.Text,
                "runs/evidence.txt"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("managed/process/evidence.txt", result.Value.ManagedStoragePath);
        Assert.Equal(recordId, result.Value.ArtifactRecordId);
        Assert.Equal(plan.ExternalReferenceKey, result.Value.ExternalReferenceKey);
        Assert.Equal(expectationId, result.Value.ArtifactExpectationId);
        Assert.NotNull(capturedRecord);
        Assert.Equal("managed/process/evidence.txt", capturedRecord.ManagedStoragePath);
        Assert.Equal(plan.ExternalReferenceKey, capturedRecord.ExternalReferenceKey);
        Assert.Equal(plan.ArtifactExpectationId, capturedRecord.ArtifactExpectationId);
        Assert.NotNull(storage.Request);
        Assert.Equal("runs/evidence.txt", storage.Request.RelativePathHint);
    }

    [Fact]
    public async Task WriteAsync_SB03_INV_002_returns_record_errors_without_success_outcome_when_recording_fails()
    {
        var storage = new RecordingStoragePlacementService("managed/process/evidence.txt");
        var coordinator = new ProcessArtifactProjectionWriteCoordinator(
            storage,
            (_, _) => Task.FromResult(Result<Guid>.Failure(Error.Failure("record failed", "record_failed"))));

        var result = await coordinator.WriteAsync(
            new ProcessArtifactProjectionWriteRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                CreatePlan(null, "agentframework-artifact:source", "agentframework-artifact:source"),
                "evidence.txt",
                "text/plain",
                "hello"u8.ToArray(),
                StorageContentKind.Text,
                "runs/evidence.txt"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Collection(result.Errors, error => {
            Assert.Equal("record_failed", error.Code);
            Assert.Equal("record failed", error.Message);
        });
        Assert.NotNull(storage.Request);
    }

    private static ProcessArtifactProjectionPlan CreatePlan(
        Guid? expectationId,
        string sourceExternalReferenceKey,
        string externalReferenceKey)
    {
        return new ProcessArtifactProjectionPlan(
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact,
            sourceExternalReferenceKey,
            externalReferenceKey,
            expectationId,
            ProcessArtifactKind.Evidence,
            "Evidence",
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Projected from a test execution artifact.",
            "Process evidence and audit review.",
            "Review generated evidence.",
            new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.AgentExecutionArtifact,
                SourceExecutionRunId = Guid.NewGuid(),
                SourceExternalReferenceKey = sourceExternalReferenceKey
            });
    }

    private sealed class RecordingStoragePlacementService(string relativePath) : IStoragePlacementService
    {
        public StoragePlacementRequest? Request { get; private set; }

        public Task<StoragePlacementResult> PlaceAsync(
            StoragePlacementRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Test storage",
                ProviderKind = StorageProviderKind.FileSystem,
                CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Download | StorageCapability.InlinePreview
            };
            var reference = new StorageObjectReference(
                storage.Id,
                storage.ProviderKind,
                StorageLocatorKind.RelativePath,
                relativePath,
                request.FileName,
                request.ContentType,
                request.Content.LongLength);
            var access = new StorageAccessDescriptor(
                "/storage/objects/preview?ref=test",
                "/storage/objects/download?ref=test",
                null,
                true,
                true,
                false,
                request.FileName,
                request.ContentType,
                request.Content.LongLength,
                string.Empty);
            var recommendation = new StorageRecommendation(
                new StorageRecommendationCandidate(
                    storage.Id,
                    storage.Name,
                    storage.ProviderKind,
                    storage.CapabilityMask,
                    StorageHealthStatus.Healthy,
                    false,
                    "Test storage."),
                [],
                "Test storage.",
                []);

            return Task.FromResult(new StoragePlacementResult(
                storage,
                recommendation,
                new StorageWriteResult(reference, access),
                access.PreviewUrl,
                Path.Combine(Path.GetTempPath(), request.FileName),
                relativePath));
        }
    }
}
