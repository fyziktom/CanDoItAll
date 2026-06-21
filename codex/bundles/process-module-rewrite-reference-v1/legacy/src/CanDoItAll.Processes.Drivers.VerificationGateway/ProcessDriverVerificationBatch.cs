using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.VerificationGateway;

public sealed record ProcessDriverVerificationBatchAggregationRequest(
    DateTimeOffset RequestedAt,
    string CallerContext);

public sealed record ProcessDriverVerificationBatchRequest
{
    public ProcessDriverVerificationBatchRequest(
        IReadOnlyList<TranscriptVerificationAlphaRequest>? transcriptRequests = null,
        IReadOnlyList<RuntimeEvidenceConsistencyVerificationRequest>? runtimeEvidenceRequests = null,
        IReadOnlyList<ArtifactEvidenceVerificationRequest>? artifactEvidenceRequests = null,
        IReadOnlyList<OfficeEvidenceVerificationRequest>? officeEvidenceRequests = null,
        IReadOnlyList<BusinessAnalysisVerificationRequest>? businessAnalysisRequests = null,
        ProcessDriverVerificationBatchAggregationRequest? aggregation = null)
    {
        TranscriptRequests = CreateReadonlyList(transcriptRequests);
        RuntimeEvidenceRequests = CreateReadonlyList(runtimeEvidenceRequests);
        ArtifactEvidenceRequests = CreateReadonlyList(artifactEvidenceRequests);
        OfficeEvidenceRequests = CreateReadonlyList(officeEvidenceRequests);
        BusinessAnalysisRequests = CreateReadonlyList(businessAnalysisRequests);
        Aggregation = aggregation;
    }

    public IReadOnlyList<TranscriptVerificationAlphaRequest> TranscriptRequests { get; }

    public IReadOnlyList<RuntimeEvidenceConsistencyVerificationRequest> RuntimeEvidenceRequests { get; }

    public IReadOnlyList<ArtifactEvidenceVerificationRequest> ArtifactEvidenceRequests { get; }

    public IReadOnlyList<OfficeEvidenceVerificationRequest> OfficeEvidenceRequests { get; }

    public IReadOnlyList<BusinessAnalysisVerificationRequest> BusinessAnalysisRequests { get; }

    public ProcessDriverVerificationBatchAggregationRequest? Aggregation { get; }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IReadOnlyList<T>? values)
    {
        return Array.AsReadOnly((values ?? []).ToArray());
    }
}

public sealed record ProcessDriverVerificationBatchResponse
{
    public ProcessDriverVerificationBatchResponse(
        IReadOnlyList<ProcessDriverVerificationResponse> transcriptResponses,
        IReadOnlyList<ProcessDriverVerificationResponse> runtimeEvidenceResponses,
        IReadOnlyList<ProcessDriverVerificationResponse> artifactEvidenceResponses,
        IReadOnlyList<ProcessDriverVerificationResponse> officeEvidenceResponses,
        IReadOnlyList<ProcessDriverVerificationResponse> businessAnalysisResponses,
        ProcessDriverObservationAggregate? aggregate)
    {
        TranscriptResponses = CreateReadonlyList(transcriptResponses);
        RuntimeEvidenceResponses = CreateReadonlyList(runtimeEvidenceResponses);
        ArtifactEvidenceResponses = CreateReadonlyList(artifactEvidenceResponses);
        OfficeEvidenceResponses = CreateReadonlyList(officeEvidenceResponses);
        BusinessAnalysisResponses = CreateReadonlyList(businessAnalysisResponses);
        Aggregate = aggregate;
        AllResponses = CreateReadonlyList(
            TranscriptResponses
                .Concat(RuntimeEvidenceResponses)
                .Concat(ArtifactEvidenceResponses)
                .Concat(OfficeEvidenceResponses)
                .Concat(BusinessAnalysisResponses));
    }

    public IReadOnlyList<ProcessDriverVerificationResponse> TranscriptResponses { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> RuntimeEvidenceResponses { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> ArtifactEvidenceResponses { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> OfficeEvidenceResponses { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> BusinessAnalysisResponses { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> AllResponses { get; }

    public ProcessDriverObservationAggregate? Aggregate { get; }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IEnumerable<T> values)
    {
        return Array.AsReadOnly(values.ToArray());
    }
}
