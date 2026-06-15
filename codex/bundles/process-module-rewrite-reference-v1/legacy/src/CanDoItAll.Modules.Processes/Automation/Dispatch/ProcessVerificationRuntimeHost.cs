using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessVerificationRuntimeHost
{
    Task<ProcessVerificationHostResult> VerifyAsync(
        ProcessVerificationHostRequest request,
        CancellationToken cancellationToken = default);

    ProcessVerificationHostResponse Verify(ProcessVerificationHostRequest request);
}

internal sealed class ProcessVerificationRuntimeHost : IProcessVerificationRuntimeHost
{
    private readonly ProcessReadOnlyVerificationBatchOrchestrator orchestrator;
    private readonly ProcessVerificationLaneSelector selector;
    private readonly IProcessVerificationAuditStore auditStore;
    private readonly ProcessVerificationRuntimeHostOptions options;

    public ProcessVerificationRuntimeHost(
        ProcessReadOnlyVerificationBatchOrchestrator orchestrator,
        ProcessVerificationLaneSelector selector,
        IProcessVerificationAuditStore auditStore,
        IOptions<ProcessVerificationRuntimeHostOptions> options)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ProcessVerificationHostResponse Verify(ProcessVerificationHostRequest request)
    {
        var result = VerifyAsync(request).GetAwaiter().GetResult();
        if (result.Response is not null)
        {
            return result.Response;
        }

        var denial = result.Denial ?? throw new InvalidOperationException("Verification host returned neither response nor denial.");
        if (denial.Code == ProcessVerificationHostDenialCode.UnsupportedLane)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Lane, denial.Message);
        }

        throw new InvalidOperationException(denial.Message);
    }

    public async Task<ProcessVerificationHostResult> VerifyAsync(
        ProcessVerificationHostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Enabled)
        {
            return await DenyAsync(
                request,
                ProcessVerificationHostDenialCode.HostDisabled,
                "Process verification runtime host is disabled by options.",
                cancellationToken);
        }

        var selection = selector.SelectExact(request.Lane);
        if (selection.Status == ProcessVerificationLaneSelectionStatus.UnsupportedLane)
        {
            return await DenyAsync(
                request,
                ProcessVerificationHostDenialCode.UnsupportedLane,
                $"Unsupported verification lane {request.Lane}.",
                cancellationToken);
        }

        if (selection.Status == ProcessVerificationLaneSelectionStatus.MissingRegistration)
        {
            return await DenyAsync(
                request,
                ProcessVerificationHostDenialCode.MissingLaneRegistration,
                $"No verification lane registration exists for lane {request.Lane}.",
                cancellationToken);
        }

        var registration = selection.Registration ??
            throw new InvalidOperationException($"Verification lane selection for {request.Lane} returned no registration.");

        if (!options.IsLaneEnabled(request.Lane))
        {
            return await DenyAsync(
                request,
                ProcessVerificationHostDenialCode.LaneDisabled,
                $"Verification lane {request.Lane} is disabled by options.",
                cancellationToken);
        }

        var lanePayloadResult = CreateLanePayload(request);
        if (lanePayloadResult.DenialCode.HasValue)
        {
            return await DenyAsync(
                request,
                lanePayloadResult.DenialCode.Value,
                lanePayloadResult.DenialMessage,
                cancellationToken);
        }

        var lanePayload = lanePayloadResult.Payload ??
            throw new InvalidOperationException($"Verification lane payload creation for {request.Lane} returned no payload.");
        var observation = orchestrator.Verify(lanePayload!);
        if (observation.ResponseCount == 0)
        {
            return await DenyAsync(
                request,
                ProcessVerificationHostDenialCode.NoResponsesProduced,
                $"No responses were produced for lane {request.Lane}.",
                cancellationToken);
        }

        var noMutationPerformed = observation.Responses.All(response => response.NoMutationPerformed) &&
            (observation.AggregateObservation?.AllResponsesMutationFree ?? true);
        var auditRecord = await auditStore.AppendAsync(CreateAuditRecord(
            request,
            observation,
            noMutationPerformed),
            cancellationToken);

        return ProcessVerificationHostResult.Succeeded(new ProcessVerificationHostResponse(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(request.Lane),
            request.Lane,
            registration,
            observation,
            auditRecord,
            noMutationPerformed,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false));
    }

    private ProcessVerificationLanePayloadResult CreateLanePayload(ProcessVerificationHostRequest request)
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
            _ => ProcessVerificationLanePayloadResult.Denied(
                ProcessVerificationHostDenialCode.UnsupportedLane,
                $"Unsupported verification lane {request.Lane}.")
        };
    }

    private ProcessVerificationLanePayloadResult CreateRequiredLanePayload(
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
            return ProcessVerificationLanePayloadResult.Denied(
                ProcessVerificationHostDenialCode.MissingLanePayload,
                $"No payloads were supplied for lane {request.Lane} on process run {request.Payload.ProcessRunId}.");
        }

        if (lanePayloadCount > options.MaxPayloadItemsPerLane)
        {
            return ProcessVerificationLanePayloadResult.Denied(
                ProcessVerificationHostDenialCode.PayloadLimitExceeded,
                $"Lane {request.Lane} supplied {lanePayloadCount} payload item(s), exceeding the limit of {options.MaxPayloadItemsPerLane}.");
        }

        var suppliedEvidenceContentBytes = ResolveLanePayloadMaterialBytes(request);
        if (suppliedEvidenceContentBytes > options.MaxSuppliedEvidenceContentBytes)
        {
            return ProcessVerificationLanePayloadResult.Denied(
                ProcessVerificationHostDenialCode.SuppliedEvidenceContentLimitExceeded,
                $"Lane {request.Lane} supplied {suppliedEvidenceContentBytes} evidence content byte(s), exceeding the limit of {options.MaxSuppliedEvidenceContentBytes}.");
        }

        return ProcessVerificationLanePayloadResult.Created(new ProcessReadOnlyVerificationBatchPayload(
            request.Payload.ProcessRunId,
            request.Payload.StepRunId,
            request.Payload.CallerContext,
            request.RequestedAt,
            transcriptPayloads,
            runtimeEvidencePayloads,
            artifactEvidencePayloads,
            officeEvidencePayloads,
            businessAnalysisPayloads));
    }

    private static long ResolveLanePayloadMaterialBytes(ProcessVerificationHostRequest request)
    {
        var payload = request.Payload;
        return request.Lane switch
        {
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification => payload.TranscriptPayloads.Sum(
                item => Encoding.UTF8.GetByteCount(item.TranscriptText ?? string.Empty)),
            ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency => payload.RuntimeEvidencePayloads.Sum(
                item => SumEvidenceReferenceMaterialBytes(item.EvidenceReferences)),
            ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency => payload.ArtifactEvidencePayloads.Sum(
                item => item.SuppliedContent?.SizeBytes ?? 0),
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead => payload.OfficeEvidencePayloads.Sum(
                item => item.SuppliedContent?.SizeBytes ?? 0),
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead => payload.BusinessAnalysisPayloads.Sum(
                item => item.SuppliedContent?.SizeBytes ?? 0),
            _ => 0
        };
    }

    private static long SumEvidenceReferenceMaterialBytes(IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        return evidenceReferences.Sum(reference =>
            Encoding.UTF8.GetByteCount(reference.Uri ?? string.Empty) +
            Encoding.UTF8.GetByteCount(reference.ContentHash ?? string.Empty));
    }

    private async Task<ProcessVerificationHostResult> DenyAsync(
        ProcessVerificationHostRequest request,
        ProcessVerificationHostDenialCode code,
        string message,
        CancellationToken cancellationToken)
    {
        return ProcessVerificationHostResult.Denied(await CreateDenialAsync(request, code, message, cancellationToken));
    }

    private async Task<ProcessVerificationHostDenial> CreateDenialAsync(
        ProcessVerificationHostRequest request,
        ProcessVerificationHostDenialCode code,
        string message,
        CancellationToken cancellationToken)
    {
        var auditRecord = await auditStore.AppendAsync(new ProcessVerificationAuditRecord(
            Guid.NewGuid(),
            ProcessReadOnlyObservationClock.ObservedAt(request.RequestedAt),
            request.Payload.ProcessRunId,
            request.Payload.StepRunId,
            request.RequestedBy,
            request.Lane,
            ResponseCount: 0,
            AcceptedCount: 0,
            DeniedCount: 1,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false,
            ComputeDenialHash(request, code, message)),
            cancellationToken);

        return new ProcessVerificationHostDenial(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(request.Lane),
            ProcessVerificationHostDenialClassifier.Classify(code),
            code,
            message,
            request.Lane,
            request.Payload.ProcessRunId,
            request.Payload.StepRunId,
            auditRecord.RequestedBy,
            request.RequestedAt,
            auditRecord,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false);
    }

    private readonly record struct ProcessVerificationLanePayloadResult(
        ProcessReadOnlyVerificationBatchPayload? Payload,
        ProcessVerificationHostDenialCode? DenialCode,
        string DenialMessage)
    {
        public static ProcessVerificationLanePayloadResult Created(ProcessReadOnlyVerificationBatchPayload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return new ProcessVerificationLanePayloadResult(payload, DenialCode: null, DenialMessage: string.Empty);
        }

        public static ProcessVerificationLanePayloadResult Denied(
            ProcessVerificationHostDenialCode denialCode,
            string denialMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(denialMessage);
            return new ProcessVerificationLanePayloadResult(Payload: null, denialCode, denialMessage);
        }
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

    private static string ComputeDenialHash(
        ProcessVerificationHostRequest request,
        ProcessVerificationHostDenialCode code,
        string message)
    {
        var payload = string.Join(
            "|",
            request.Lane,
            request.Payload.ProcessRunId,
            request.Payload.StepRunId,
            code,
            message,
            true,
            false,
            false,
            false);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
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
