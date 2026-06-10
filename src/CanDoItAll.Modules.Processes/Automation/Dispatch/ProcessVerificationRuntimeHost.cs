using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessVerificationRuntimeHost
{
    ProcessVerificationHostResponse Verify(ProcessVerificationHostRequest request);
}

internal sealed class ProcessVerificationRuntimeHost : IProcessVerificationRuntimeHost
{
    private readonly ProcessReadOnlyVerificationBatchOrchestrator orchestrator;
    private readonly ProcessVerificationLaneSelector selector;
    private readonly IProcessVerificationAuditStore auditStore;

    public ProcessVerificationRuntimeHost()
        : this(
            new ProcessReadOnlyVerificationBatchOrchestrator(),
            new ProcessVerificationLaneSelector(new ProcessVerificationLaneRegistry()),
            new InMemoryProcessVerificationAuditStore())
    {
    }

    public ProcessVerificationRuntimeHost(
        ProcessReadOnlyVerificationBatchOrchestrator orchestrator,
        ProcessVerificationLaneSelector selector,
        IProcessVerificationAuditStore auditStore)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public ProcessVerificationHostResponse Verify(ProcessVerificationHostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var registration = selector.Select(request.Lane);
        var lanePayload = CreateLanePayload(request);
        var observation = orchestrator.Verify(lanePayload);
        if (observation.ResponseCount == 0)
        {
            throw new InvalidOperationException($"No responses were produced for lane {request.Lane}.");
        }

        var noMutationPerformed = observation.Responses.All(response => response.NoMutationPerformed) &&
            (observation.AggregateObservation?.AllResponsesMutationFree ?? true);
        var auditRecord = auditStore.Append(CreateAuditRecord(
            request,
            observation,
            noMutationPerformed));

        return new ProcessVerificationHostResponse(
            request.Lane,
            registration,
            observation,
            auditRecord,
            noMutationPerformed,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false);
    }

    private static ProcessReadOnlyVerificationBatchPayload CreateLanePayload(ProcessVerificationHostRequest request)
    {
        var payload = request.Payload;
        return request.Lane switch
        {
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification => CreateRequiredLanePayload(
                request,
                payload.TranscriptPayloads.Count,
                transcriptPayloads: payload.TranscriptPayloads),
            ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency => CreateRequiredLanePayload(
                request,
                payload.RuntimeEvidencePayloads.Count,
                runtimeEvidencePayloads: payload.RuntimeEvidencePayloads),
            ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency => CreateRequiredLanePayload(
                request,
                payload.ArtifactEvidencePayloads.Count,
                artifactEvidencePayloads: payload.ArtifactEvidencePayloads),
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead => CreateRequiredLanePayload(
                request,
                payload.OfficeEvidencePayloads.Count,
                officeEvidencePayloads: payload.OfficeEvidencePayloads),
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead => CreateRequiredLanePayload(
                request,
                payload.BusinessAnalysisPayloads.Count,
                businessAnalysisPayloads: payload.BusinessAnalysisPayloads),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Lane, "Unsupported verification lane.")
        };
    }

    private static ProcessReadOnlyVerificationBatchPayload CreateRequiredLanePayload(
        ProcessVerificationHostRequest request,
        int lanePayloadCount,
        IReadOnlyList<ProcessTranscriptVerificationReadOnlyEvidencePayload>? transcriptPayloads = null,
        IReadOnlyList<ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload>? runtimeEvidencePayloads = null,
        IReadOnlyList<ProcessArtifactEvidenceReadOnlyPayload>? artifactEvidencePayloads = null,
        IReadOnlyList<ProcessOfficeEvidenceReadOnlyPayload>? officeEvidencePayloads = null,
        IReadOnlyList<ProcessBusinessAnalysisReadOnlyPayload>? businessAnalysisPayloads = null)
    {
        if (lanePayloadCount == 0)
        {
            throw new InvalidOperationException(
                $"No payloads were supplied for lane {request.Lane} on process run {request.Payload.ProcessRunId}.");
        }

        return new ProcessReadOnlyVerificationBatchPayload(
            request.Payload.ProcessRunId,
            request.Payload.StepRunId,
            request.Payload.CallerContext,
            request.RequestedAt,
            transcriptPayloads,
            runtimeEvidencePayloads,
            artifactEvidencePayloads,
            officeEvidencePayloads,
            businessAnalysisPayloads);
    }

    private static ProcessVerificationAuditRecord CreateAuditRecord(
        ProcessVerificationHostRequest request,
        ProcessReadOnlyVerificationBatchObservation observation,
        bool noMutationPerformed)
    {
        var acceptedCount = observation.Responses.Count(response => response.Accepted);
        var deniedCount = observation.Responses.Count - acceptedCount;
        return new ProcessVerificationAuditRecord(
            Guid.NewGuid(),
            ProcessReadOnlyObservationClock.ObservedAt(request.RequestedAt),
            observation.ProcessRunId,
            observation.StepRunId,
            request.RequestedBy,
            request.Lane,
            observation.ResponseCount,
            acceptedCount,
            deniedCount,
            noMutationPerformed,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false,
            ComputeObservationHash(request, observation, noMutationPerformed));
    }

    private static string ComputeObservationHash(
        ProcessVerificationHostRequest request,
        ProcessReadOnlyVerificationBatchObservation observation,
        bool noMutationPerformed)
    {
        var evidenceHashes = observation.Responses
            .SelectMany(response => response.EvidenceReferences)
            .Select(reference => reference.ContentHash)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var payload = string.Join(
            "|",
            request.Lane,
            observation.ProcessRunId,
            observation.StepRunId,
            observation.ResponseCount,
            observation.Responses.Count(response => response.Accepted),
            noMutationPerformed,
            string.Join(",", evidenceHashes));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
