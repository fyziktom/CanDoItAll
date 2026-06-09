using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Processes.Drivers.VerificationGateway;

public sealed class ProcessDriverVerificationGateway
{
    private static readonly IReadOnlyList<ProcessDriverVerificationGatewayLaneDescriptor> ImplementedLaneDescriptors =
    [
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification),
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency),
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency),
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead),
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead)
    ];

    private readonly TranscriptVerificationAlphaVerifier transcriptVerifier;
    private readonly RuntimeEvidenceConsistencyAlphaVerifier runtimeEvidenceVerifier;
    private readonly ArtifactEvidenceAlphaVerifier artifactEvidenceVerifier;
    private readonly OfficeEvidenceAlphaVerifier officeEvidenceVerifier;
    private readonly BusinessAnalysisAlphaVerifier businessAnalysisVerifier;
    private readonly ProcessDriverObservationAggregator observationAggregator;

    public ProcessDriverVerificationGateway(
        TranscriptVerificationAlphaVerifier transcriptVerifier,
        RuntimeEvidenceConsistencyAlphaVerifier runtimeEvidenceVerifier,
        ArtifactEvidenceAlphaVerifier artifactEvidenceVerifier,
        OfficeEvidenceAlphaVerifier officeEvidenceVerifier,
        BusinessAnalysisAlphaVerifier businessAnalysisVerifier,
        ProcessDriverObservationAggregator observationAggregator)
    {
        this.transcriptVerifier = transcriptVerifier ?? throw new ArgumentNullException(nameof(transcriptVerifier));
        this.runtimeEvidenceVerifier = runtimeEvidenceVerifier ?? throw new ArgumentNullException(nameof(runtimeEvidenceVerifier));
        this.artifactEvidenceVerifier = artifactEvidenceVerifier ?? throw new ArgumentNullException(nameof(artifactEvidenceVerifier));
        this.officeEvidenceVerifier = officeEvidenceVerifier ?? throw new ArgumentNullException(nameof(officeEvidenceVerifier));
        this.businessAnalysisVerifier = businessAnalysisVerifier ?? throw new ArgumentNullException(nameof(businessAnalysisVerifier));
        this.observationAggregator = observationAggregator ?? throw new ArgumentNullException(nameof(observationAggregator));
    }

    public IReadOnlyList<ProcessDriverVerificationGatewayLaneDescriptor> ImplementedLanes => ImplementedLaneDescriptors;

    public static ProcessDriverVerificationGateway CreateDefault()
    {
        return new ProcessDriverVerificationGateway(
            new TranscriptVerificationAlphaVerifier(),
            new RuntimeEvidenceConsistencyAlphaVerifier(),
            new ArtifactEvidenceAlphaVerifier(),
            new OfficeEvidenceAlphaVerifier(),
            new BusinessAnalysisAlphaVerifier(),
            new ProcessDriverObservationAggregator());
    }

    public ProcessDriverVerificationResponse VerifyTranscript(TranscriptVerificationAlphaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return transcriptVerifier.Verify(request);
    }

    public ProcessDriverVerificationResponse VerifyRuntimeEvidence(RuntimeEvidenceConsistencyVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return runtimeEvidenceVerifier.Verify(request);
    }

    public ProcessDriverVerificationResponse VerifyArtifactEvidence(ArtifactEvidenceVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return artifactEvidenceVerifier.Verify(request);
    }

    public ProcessDriverVerificationResponse VerifyOfficeEvidence(OfficeEvidenceVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return officeEvidenceVerifier.Verify(request);
    }

    public ProcessDriverVerificationResponse VerifyBusinessAnalysis(BusinessAnalysisVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return businessAnalysisVerifier.Verify(request);
    }

    public ProcessDriverObservationAggregate AggregateObservations(ProcessDriverObservationAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return observationAggregator.Aggregate(request);
    }

    public ProcessDriverVerificationBatchResponse VerifyBatch(ProcessDriverVerificationBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transcriptResponses = VerifyTranscriptBatch(request.TranscriptRequests);
        var runtimeEvidenceResponses = VerifyRuntimeEvidenceBatch(request.RuntimeEvidenceRequests);
        var artifactEvidenceResponses = VerifyArtifactEvidenceBatch(request.ArtifactEvidenceRequests);
        var officeEvidenceResponses = VerifyOfficeEvidenceBatch(request.OfficeEvidenceRequests);
        var businessAnalysisResponses = VerifyBusinessAnalysisBatch(request.BusinessAnalysisRequests);
        var allResponses = transcriptResponses
            .Concat(runtimeEvidenceResponses)
            .Concat(artifactEvidenceResponses)
            .Concat(officeEvidenceResponses)
            .Concat(businessAnalysisResponses)
            .ToArray();
        var aggregate = request.Aggregation is null
            ? null
            : CreateBatchAggregate(request.Aggregation, allResponses);

        return new ProcessDriverVerificationBatchResponse(
            transcriptResponses,
            runtimeEvidenceResponses,
            artifactEvidenceResponses,
            officeEvidenceResponses,
            businessAnalysisResponses,
            aggregate);
    }

    private ProcessDriverObservationAggregate CreateBatchAggregate(
        ProcessDriverVerificationBatchAggregationRequest request,
        IReadOnlyList<ProcessDriverVerificationResponse> responses)
    {
        if (responses.Count == 0)
        {
            throw new InvalidOperationException("Batch aggregation requires at least one verification response.");
        }

        return AggregateObservations(new ProcessDriverObservationAggregationRequest(
            responses,
            request.RequestedAt,
            request.CallerContext));
    }

    private IReadOnlyList<ProcessDriverVerificationResponse> VerifyTranscriptBatch(
        IReadOnlyList<TranscriptVerificationAlphaRequest> requests)
    {
        return Array.AsReadOnly(requests.Select(VerifyTranscript).ToArray());
    }

    private IReadOnlyList<ProcessDriverVerificationResponse> VerifyRuntimeEvidenceBatch(
        IReadOnlyList<RuntimeEvidenceConsistencyVerificationRequest> requests)
    {
        return Array.AsReadOnly(requests.Select(VerifyRuntimeEvidence).ToArray());
    }

    private IReadOnlyList<ProcessDriverVerificationResponse> VerifyArtifactEvidenceBatch(
        IReadOnlyList<ArtifactEvidenceVerificationRequest> requests)
    {
        return Array.AsReadOnly(requests.Select(VerifyArtifactEvidence).ToArray());
    }

    private IReadOnlyList<ProcessDriverVerificationResponse> VerifyOfficeEvidenceBatch(
        IReadOnlyList<OfficeEvidenceVerificationRequest> requests)
    {
        return Array.AsReadOnly(requests.Select(VerifyOfficeEvidence).ToArray());
    }

    private IReadOnlyList<ProcessDriverVerificationResponse> VerifyBusinessAnalysisBatch(
        IReadOnlyList<BusinessAnalysisVerificationRequest> requests)
    {
        return Array.AsReadOnly(requests.Select(VerifyBusinessAnalysis).ToArray());
    }
}
