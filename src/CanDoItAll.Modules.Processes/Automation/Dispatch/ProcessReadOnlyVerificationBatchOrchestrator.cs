using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessReadOnlyVerificationBatchOrchestrator
{
    private readonly ProcessTranscriptVerificationReadOnlyAdapter transcriptAdapter;
    private readonly ProcessRuntimeEvidenceVerificationReadOnlyAdapter runtimeEvidenceAdapter;
    private readonly ProcessArtifactEvidenceReadOnlyAdapter artifactEvidenceAdapter;
    private readonly ProcessOfficeEvidenceReadOnlyAdapter officeEvidenceAdapter;
    private readonly ProcessBusinessAnalysisReadOnlyAdapter businessAnalysisAdapter;
    private readonly ProcessDriverObservationAggregationReadOnlyAdapter aggregationAdapter;

    public ProcessReadOnlyVerificationBatchOrchestrator()
        : this(
            new ProcessTranscriptVerificationReadOnlyAdapter(),
            new ProcessRuntimeEvidenceVerificationReadOnlyAdapter(),
            new ProcessArtifactEvidenceReadOnlyAdapter(),
            new ProcessOfficeEvidenceReadOnlyAdapter(),
            new ProcessBusinessAnalysisReadOnlyAdapter(),
            new ProcessDriverObservationAggregationReadOnlyAdapter())
    {
    }

    internal ProcessReadOnlyVerificationBatchOrchestrator(
        ProcessTranscriptVerificationReadOnlyAdapter transcriptAdapter,
        ProcessRuntimeEvidenceVerificationReadOnlyAdapter runtimeEvidenceAdapter,
        ProcessArtifactEvidenceReadOnlyAdapter artifactEvidenceAdapter,
        ProcessOfficeEvidenceReadOnlyAdapter officeEvidenceAdapter,
        ProcessBusinessAnalysisReadOnlyAdapter businessAnalysisAdapter,
        ProcessDriverObservationAggregationReadOnlyAdapter aggregationAdapter)
    {
        this.transcriptAdapter = transcriptAdapter ?? throw new ArgumentNullException(nameof(transcriptAdapter));
        this.runtimeEvidenceAdapter = runtimeEvidenceAdapter ?? throw new ArgumentNullException(nameof(runtimeEvidenceAdapter));
        this.artifactEvidenceAdapter = artifactEvidenceAdapter ?? throw new ArgumentNullException(nameof(artifactEvidenceAdapter));
        this.officeEvidenceAdapter = officeEvidenceAdapter ?? throw new ArgumentNullException(nameof(officeEvidenceAdapter));
        this.businessAnalysisAdapter = businessAnalysisAdapter ?? throw new ArgumentNullException(nameof(businessAnalysisAdapter));
        this.aggregationAdapter = aggregationAdapter ?? throw new ArgumentNullException(nameof(aggregationAdapter));
    }

    public ProcessReadOnlyVerificationBatchObservation Verify(ProcessReadOnlyVerificationBatchPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var transcriptObservations = CreateReadonlyList(
            payload.TranscriptPayloads.Select(transcriptAdapter.Verify));
        var runtimeEvidenceObservations = CreateReadonlyList(
            payload.RuntimeEvidencePayloads.Select(runtimeEvidenceAdapter.Verify));
        var artifactEvidenceObservations = CreateReadonlyList(
            payload.ArtifactEvidencePayloads.Select(artifactEvidenceAdapter.Verify));
        var officeEvidenceObservations = CreateReadonlyList(
            payload.OfficeEvidencePayloads.Select(officeEvidenceAdapter.Verify));
        var businessAnalysisObservations = CreateReadonlyList(
            payload.BusinessAnalysisPayloads.Select(businessAnalysisAdapter.Verify));
        var responses = CreateResponses(
            transcriptObservations,
            runtimeEvidenceObservations,
            artifactEvidenceObservations,
            officeEvidenceObservations,
            businessAnalysisObservations);
        var aggregateObservation = responses.Count == 0
            ? null
            : ProcessReadOnlyVerificationAggregateObservationMapper.Create(
                payload,
                aggregationAdapter.Aggregate(new ProcessDriverObservationAggregationReadOnlyPayload(
                    payload.ProcessRunId,
                    payload.StepRunId,
                    payload.CallerContext,
                    responses,
                    payload.RequestedAt)));

        return new ProcessReadOnlyVerificationBatchObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.CallerContext,
            transcriptObservations,
            runtimeEvidenceObservations,
            artifactEvidenceObservations,
            officeEvidenceObservations,
            businessAnalysisObservations,
            responses,
            aggregateObservation,
            payload.RequestedAt,
            ProcessReadOnlyObservationClock.ObservedAt(payload.RequestedAt));
    }

    private static IReadOnlyList<ProcessDriverVerificationResponse> CreateResponses(
        IReadOnlyList<ProcessTranscriptVerificationReadOnlyObservation> transcriptObservations,
        IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyObservation> runtimeEvidenceObservations,
        IReadOnlyList<ProcessArtifactEvidenceReadOnlyObservation> artifactEvidenceObservations,
        IReadOnlyList<ProcessOfficeEvidenceReadOnlyObservation> officeEvidenceObservations,
        IReadOnlyList<ProcessBusinessAnalysisReadOnlyObservation> businessAnalysisObservations)
    {
        var responses = new List<ProcessDriverVerificationResponse>(
            transcriptObservations.Count +
            runtimeEvidenceObservations.Count +
            artifactEvidenceObservations.Count +
            officeEvidenceObservations.Count +
            businessAnalysisObservations.Count);

        AddTranscriptResponses(responses, transcriptObservations);
        AddRuntimeEvidenceResponses(responses, runtimeEvidenceObservations);
        AddArtifactEvidenceResponses(responses, artifactEvidenceObservations);
        AddOfficeEvidenceResponses(responses, officeEvidenceObservations);
        AddBusinessAnalysisResponses(responses, businessAnalysisObservations);

        return Array.AsReadOnly(responses.ToArray());
    }

    private static void AddTranscriptResponses(
        List<ProcessDriverVerificationResponse> responses,
        IReadOnlyList<ProcessTranscriptVerificationReadOnlyObservation> observations)
    {
        foreach (var observation in observations)
        {
            responses.Add(CreateResponse(observation));
        }
    }

    private static void AddRuntimeEvidenceResponses(
        List<ProcessDriverVerificationResponse> responses,
        IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyObservation> observations)
    {
        foreach (var observation in observations)
        {
            responses.Add(CreateResponse(observation));
        }
    }

    private static void AddArtifactEvidenceResponses(
        List<ProcessDriverVerificationResponse> responses,
        IReadOnlyList<ProcessArtifactEvidenceReadOnlyObservation> observations)
    {
        foreach (var observation in observations)
        {
            responses.Add(CreateResponse(observation));
        }
    }

    private static void AddOfficeEvidenceResponses(
        List<ProcessDriverVerificationResponse> responses,
        IReadOnlyList<ProcessOfficeEvidenceReadOnlyObservation> observations)
    {
        foreach (var observation in observations)
        {
            responses.Add(CreateResponse(observation));
        }
    }

    private static void AddBusinessAnalysisResponses(
        List<ProcessDriverVerificationResponse> responses,
        IReadOnlyList<ProcessBusinessAnalysisReadOnlyObservation> observations)
    {
        foreach (var observation in observations)
        {
            responses.Add(CreateResponse(observation));
        }
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessTranscriptVerificationReadOnlyObservation observation)
    {
        return new ProcessDriverVerificationResponse(
            observation.Accepted,
            observation.DenialReason,
            observation.Diagnostics,
            observation.EvidenceReferences,
            observation.Redaction,
            observation.NoMutationPerformed,
            observation.AuditFacts,
            observation.ContractVersion);
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessRuntimeEvidenceVerificationReadOnlyObservation observation)
    {
        return new ProcessDriverVerificationResponse(
            observation.Accepted,
            observation.DenialReason,
            observation.Diagnostics,
            observation.EvidenceReferences,
            observation.Redaction,
            observation.NoMutationPerformed,
            observation.AuditFacts,
            observation.ContractVersion);
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessArtifactEvidenceReadOnlyObservation observation)
    {
        return new ProcessDriverVerificationResponse(
            observation.Accepted,
            observation.DenialReason,
            observation.Diagnostics,
            observation.EvidenceReferences,
            observation.Redaction,
            observation.NoMutationPerformed,
            observation.AuditFacts,
            observation.ContractVersion);
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessOfficeEvidenceReadOnlyObservation observation)
    {
        return new ProcessDriverVerificationResponse(
            observation.Accepted,
            observation.DenialReason,
            observation.Diagnostics,
            observation.EvidenceReferences,
            observation.Redaction,
            observation.NoMutationPerformed,
            observation.AuditFacts,
            observation.ContractVersion);
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessBusinessAnalysisReadOnlyObservation observation)
    {
        return new ProcessDriverVerificationResponse(
            observation.Accepted,
            observation.DenialReason,
            observation.Diagnostics,
            observation.EvidenceReferences,
            observation.Redaction,
            observation.NoMutationPerformed,
            observation.AuditFacts,
            observation.ContractVersion);
    }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IEnumerable<T> values)
    {
        return Array.AsReadOnly(values.ToArray());
    }
}

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
