using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessVerificationRuntimeHostStatusService {
    Task<ProcessVerificationRuntimeHostStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<ProcessVerificationRuntimeHostStatusDto> GetStatusAsync(
        ProcessVerificationRuntimeHostStatusRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessVerificationRuntimeHostStatusService(
    IOptions<ProcessVerificationRuntimeHostOptions> options,
    ProcessVerificationLaneRegistry laneRegistry,
    IProcessVerificationAuditStore auditStore) : IProcessVerificationRuntimeHostStatusService {
    public Task<ProcessVerificationRuntimeHostStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) {
        return GetStatusAsync(
            new ProcessVerificationRuntimeHostStatusRequest(
                correlationId: string.Empty,
                requestedBy: "system",
                requestedAt: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public Task<ProcessVerificationRuntimeHostStatusDto> GetStatusAsync(
        ProcessVerificationRuntimeHostStatusRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var hostOptions = options.Value;
        var lanes = laneRegistry.Registrations
            .OrderBy(registration => registration.Lane)
            .Select(registration => new ProcessVerificationRuntimeHostLaneStatusDto(
                registration.Lane,
                Registered: true,
                Enabled: hostOptions.IsLaneEnabled(registration.Lane),
                registration.RequiredScopeKind,
                registration.RequiredPermissionMode,
                registration.AllowedOperations))
            .ToArray();
        var auditStoreKind = ResolveAuditStoreKind(auditStore);
        var capabilities = ProcessVerificationHostCapabilityCatalog.StaticDescriptors
            .OrderBy(descriptor => descriptor.Kind)
            .ThenBy(descriptor => descriptor.Key, StringComparer.Ordinal)
            .Select(descriptor => new ProcessVerificationRuntimeHostCapabilityStatusDto(
                descriptor.Key,
                descriptor.Kind,
                descriptor.ContractSurface,
                descriptor.PermissionMode,
                descriptor.AllowedOperations,
                descriptor.DeniedOperations,
                descriptor.IsStaticReadOnlyDescriptor,
                descriptor.ReflectionDiscoveryAllowed,
                descriptor.SelfRegistrationAllowed,
                descriptor.ExecutionAllowed))
            .ToArray();
        var readiness = ResolveReadiness(hostOptions, lanes, auditStoreKind);

        return Task.FromResult(new ProcessVerificationRuntimeHostStatusDto(
            request.CorrelationId,
            request.RequestedBy,
            request.RequestedAt,
            hostOptions.Enabled,
            EmergencyDisabled: !hostOptions.Enabled,
            readiness,
            auditStoreKind,
            UsesDurableAuditStore: auditStoreKind == ProcessVerificationAuditStoreKind.DurableEfCore,
            lanes,
            capabilities,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false) {
            Contract = ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.OperatorStatus),
            SupportsAuditRetentionQuery = auditStore is IProcessVerificationAuditQueryService
        });
    }

    private static ProcessVerificationAuditStoreKind ResolveAuditStoreKind(IProcessVerificationAuditStore auditStore) {
        return auditStore switch {
            EfCoreProcessVerificationAuditStore => ProcessVerificationAuditStoreKind.DurableEfCore,
            InMemoryProcessVerificationAuditStore => ProcessVerificationAuditStoreKind.TestInMemory,
            _ => ProcessVerificationAuditStoreKind.Unknown
        };
    }

    private static ProcessVerificationRuntimeHostReadiness ResolveReadiness(
        ProcessVerificationRuntimeHostOptions options,
        IReadOnlyList<ProcessVerificationRuntimeHostLaneStatusDto> lanes,
        ProcessVerificationAuditStoreKind auditStoreKind) {
        if (!options.Enabled) {
            return ProcessVerificationRuntimeHostReadiness.EmergencyDisabled;
        }

        var registeredLaneSet = lanes.Select(lane => lane.Lane).ToHashSet();
        var missingLaneRegistration = ProcessDriverVerificationGatewayLaneRules.AllowedLanes
            .Any(descriptor => !registeredLaneSet.Contains(descriptor.Lane));
        if (missingLaneRegistration) {
            return ProcessVerificationRuntimeHostReadiness.MissingLaneRegistration;
        }

        return auditStoreKind == ProcessVerificationAuditStoreKind.Unknown
            ? ProcessVerificationRuntimeHostReadiness.AuditStoreNotClassified
            : ProcessVerificationRuntimeHostReadiness.Ready;
    }
}

internal sealed record ProcessVerificationRuntimeHostStatusRequest
{
    public ProcessVerificationRuntimeHostStatusRequest(
        string? correlationId,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("Runtime host status request requires a requester identity.", nameof(requestedBy));
        }

        CorrelationId = correlationId?.Trim() ?? string.Empty;
        RequestedBy = requestedBy.Trim();
        RequestedAt = requestedAt;
    }

    public string CorrelationId { get; }

    public string RequestedBy { get; }

    public DateTimeOffset RequestedAt { get; }
}

internal enum ProcessVerificationRuntimeHostReadiness {
    Ready = 1,
    EmergencyDisabled = 2,
    MissingLaneRegistration = 3,
    AuditStoreNotClassified = 4
}

internal enum ProcessVerificationAuditStoreKind {
    Unknown = 0,
    DurableEfCore = 1,
    TestInMemory = 2
}

internal sealed record ProcessVerificationRuntimeHostStatusDto(
    string CorrelationId,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    bool Enabled,
    bool EmergencyDisabled,
    ProcessVerificationRuntimeHostReadiness Readiness,
    ProcessVerificationAuditStoreKind AuditStoreKind,
    bool UsesDurableAuditStore,
    IReadOnlyList<ProcessVerificationRuntimeHostLaneStatusDto> Lanes,
    IReadOnlyList<ProcessVerificationRuntimeHostCapabilityStatusDto> Capabilities,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation) {
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.OperatorStatus);

    public bool SupportsAuditRetentionQuery { get; init; }
}

internal sealed record ProcessVerificationRuntimeHostLaneStatusDto(
    ProcessDriverVerificationGatewayLane Lane,
    bool Registered,
    bool Enabled,
    ProcessDriverCapabilityScopeKind RequiredScopeKind,
    ProcessDriverPermissionMode RequiredPermissionMode,
    IReadOnlyList<ProcessDriverOperation> AllowedOperations);

internal sealed record ProcessVerificationRuntimeHostCapabilityStatusDto(
    string Key,
    ProcessVerificationHostCapabilityKind Kind,
    ProcessRuntimeHostContractSurface ContractSurface,
    ProcessDriverPermissionMode PermissionMode,
    IReadOnlyList<ProcessDriverOperation> AllowedOperations,
    IReadOnlyList<ProcessDriverOperation> DeniedOperations,
    bool IsStaticReadOnlyDescriptor,
    bool ReflectionDiscoveryAllowed,
    bool SelfRegistrationAllowed,
    bool ExecutionAllowed);
