using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessReadOnlyVerificationBatchPayload
{
    public ProcessReadOnlyVerificationBatchPayload(
        Guid processRunId,
        Guid stepRunId,
        string callerContext,
        DateTimeOffset requestedAt,
        IReadOnlyList<ProcessTranscriptVerificationReadOnlyEvidencePayload>? transcriptPayloads = null,
        IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload>? runtimeEvidencePayloads = null,
        IReadOnlyList<ProcessArtifactEvidenceReadOnlyPayload>? artifactEvidencePayloads = null,
        IReadOnlyList<ProcessOfficeEvidenceReadOnlyPayload>? officeEvidencePayloads = null,
        IReadOnlyList<ProcessBusinessAnalysisReadOnlyPayload>? businessAnalysisPayloads = null)
    {
        if (string.IsNullOrWhiteSpace(callerContext))
        {
            throw new ArgumentException("Caller context is required.", nameof(callerContext));
        }

        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        CallerContext = callerContext.Trim();
        RequestedAt = requestedAt;
        TranscriptPayloads = CreateReadonlyList(transcriptPayloads);
        RuntimeEvidencePayloads = CreateReadonlyList(runtimeEvidencePayloads);
        ArtifactEvidencePayloads = CreateReadonlyList(artifactEvidencePayloads);
        OfficeEvidencePayloads = CreateReadonlyList(officeEvidencePayloads);
        BusinessAnalysisPayloads = CreateReadonlyList(businessAnalysisPayloads);
    }

    public Guid ProcessRunId { get; }

    public Guid StepRunId { get; }

    public string CallerContext { get; }

    public DateTimeOffset RequestedAt { get; }

    public IReadOnlyList<ProcessTranscriptVerificationReadOnlyEvidencePayload> TranscriptPayloads { get; }

    public IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload> RuntimeEvidencePayloads { get; }

    public IReadOnlyList<ProcessArtifactEvidenceReadOnlyPayload> ArtifactEvidencePayloads { get; }

    public IReadOnlyList<ProcessOfficeEvidenceReadOnlyPayload> OfficeEvidencePayloads { get; }

    public IReadOnlyList<ProcessBusinessAnalysisReadOnlyPayload> BusinessAnalysisPayloads { get; }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IReadOnlyList<T>? values)
    {
        return Array.AsReadOnly((values ?? []).ToArray());
    }
}

internal sealed record ProcessReadOnlyVerificationBatchObservation
{
    public ProcessReadOnlyVerificationBatchObservation(
        Guid processRunId,
        Guid stepRunId,
        string callerContext,
        IReadOnlyList<ProcessTranscriptVerificationReadOnlyObservation> transcriptObservations,
        IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyObservation> runtimeEvidenceObservations,
        IReadOnlyList<ProcessArtifactEvidenceReadOnlyObservation> artifactEvidenceObservations,
        IReadOnlyList<ProcessOfficeEvidenceReadOnlyObservation> officeEvidenceObservations,
        IReadOnlyList<ProcessBusinessAnalysisReadOnlyObservation> businessAnalysisObservations,
        IReadOnlyList<ProcessDriverVerificationResponse> responses,
        ProcessReadOnlyVerificationAggregateObservation? aggregateObservation,
        DateTimeOffset requestedAt,
        DateTimeOffset observedAt)
    {
        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        CallerContext = callerContext;
        TranscriptObservations = CreateReadonlyList(transcriptObservations);
        RuntimeEvidenceObservations = CreateReadonlyList(runtimeEvidenceObservations);
        ArtifactEvidenceObservations = CreateReadonlyList(artifactEvidenceObservations);
        OfficeEvidenceObservations = CreateReadonlyList(officeEvidenceObservations);
        BusinessAnalysisObservations = CreateReadonlyList(businessAnalysisObservations);
        Responses = CreateReadonlyList(responses);
        AggregateObservation = aggregateObservation;
        RequestedAt = requestedAt;
        ObservedAt = observedAt;
    }

    public Guid ProcessRunId { get; }

    public Guid StepRunId { get; }

    public string CallerContext { get; }

    public IReadOnlyList<ProcessTranscriptVerificationReadOnlyObservation> TranscriptObservations { get; }

    public IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyObservation> RuntimeEvidenceObservations { get; }

    public IReadOnlyList<ProcessArtifactEvidenceReadOnlyObservation> ArtifactEvidenceObservations { get; }

    public IReadOnlyList<ProcessOfficeEvidenceReadOnlyObservation> OfficeEvidenceObservations { get; }

    public IReadOnlyList<ProcessBusinessAnalysisReadOnlyObservation> BusinessAnalysisObservations { get; }

    public IReadOnlyList<ProcessDriverVerificationResponse> Responses { get; }

    public ProcessReadOnlyVerificationAggregateObservation? AggregateObservation { get; }

    public DateTimeOffset RequestedAt { get; }

    public DateTimeOffset ObservedAt { get; }

    public int ResponseCount => Responses.Count;

    private static IReadOnlyList<T> CreateReadonlyList<T>(IEnumerable<T> values)
    {
        return Array.AsReadOnly(values.ToArray());
    }
}
