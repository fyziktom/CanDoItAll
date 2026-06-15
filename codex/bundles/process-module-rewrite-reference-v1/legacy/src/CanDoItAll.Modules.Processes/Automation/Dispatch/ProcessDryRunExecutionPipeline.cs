using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDryRunExecutionPipeline(
    ProcessDryRunExecutionRequestNormalizer requestNormalizer,
    ProcessDryRunExecutionCapabilityResolver capabilityResolver,
    ProcessDryRunExecutionSandboxEvaluator sandboxEvaluator,
    ProcessDryRunExecutionAuthorizationEvaluator authorizationEvaluator,
    ProcessDryRunExecutionPlanBuilder planBuilder,
    ProcessDryRunExecutionAuditMapper auditMapper)
{
    public static ProcessDryRunExecutionPipeline CreateDefault(ProcessExecutionCapableDriverFutureGate futureGate)
    {
        ArgumentNullException.ThrowIfNull(futureGate);

        return new ProcessDryRunExecutionPipeline(
            new ProcessDryRunExecutionRequestNormalizer(),
            new ProcessDryRunExecutionCapabilityResolver(),
            new ProcessDryRunExecutionSandboxEvaluator(futureGate),
            new ProcessDryRunExecutionAuthorizationEvaluator(),
            new ProcessDryRunExecutionPlanBuilder(),
            new ProcessDryRunExecutionAuditMapper());
    }

    public ProcessDryRunExecutionHostResult Evaluate(
        ProcessDryRunExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = requestNormalizer.Normalize(request);
        var capability = capabilityResolver.Resolve(normalizedRequest);
        var gateResult = sandboxEvaluator.Evaluate(normalizedRequest);
        var authorizationGaps = authorizationEvaluator.Evaluate(normalizedRequest);
        var deniedSurfaces = normalizedRequest.RequestedSurfaces
            .Where(surface => !gateResult.Allows(surface))
            .Distinct()
            .ToArray();
        var deniedOperations = normalizedRequest.RequestedSideEffectOperations
            .Where(operation => ProcessExecutionCapableDriverSurfaceMatrix
                .ResolveSurfacesForOperations([operation])
                .Any(surface => deniedSurfaces.Contains(surface)))
            .Distinct()
            .ToArray();
        var plan = planBuilder.Build(normalizedRequest, gateResult, deniedSurfaces, deniedOperations);
        var decision = deniedSurfaces.Length == 0 && deniedOperations.Length == 0
            ? ProcessDryRunExecutionHostDecision.DryRunPlanCreated
            : ProcessDryRunExecutionHostDecision.Denied;

        return new ProcessDryRunExecutionHostResult(
            capability.Key,
            normalizedRequest.Request.RequestId,
            normalizedRequest.Request.ProcessRunId,
            normalizedRequest.Request.StepRunId,
            normalizedRequest.Request.RequestedBy,
            normalizedRequest.Request.RequestedAt,
            decision,
            gateResult,
            plan,
            deniedSurfaces,
            deniedOperations,
            authorizationGaps,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false)
        {
            Contract = auditMapper.Map(
                normalizedRequest,
                capability,
                gateResult,
                deniedSurfaces,
                deniedOperations,
                authorizationGaps,
                decision)
        };
    }
}

internal sealed class ProcessDryRunExecutionRequestNormalizer
{
    public ProcessDryRunExecutionNormalizedRequest Normalize(ProcessDryRunExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedSurfaces = request.ResolveRequestedSurfaces()
            .Distinct()
            .ToArray();
        var requestedSideEffectOperations = request.RequestedOperations
            .Where(ProcessDriverOperationRules.IsSideEffectOperation)
            .Distinct()
            .ToArray();

        return new ProcessDryRunExecutionNormalizedRequest(
            request,
            requestedSurfaces,
            requestedSideEffectOperations);
    }
}

internal sealed record ProcessDryRunExecutionNormalizedRequest(
    ProcessDryRunExecutionRequest Request,
    IReadOnlyList<ProcessExecutionCapableDriverSurface> RequestedSurfaces,
    IReadOnlyList<ProcessDriverOperation> RequestedSideEffectOperations);

internal sealed class ProcessDryRunExecutionCapabilityResolver(
    IProcessVerificationHostCapabilityProvider capabilityProvider)
{
    public ProcessDryRunExecutionCapabilityResolver()
        : this(ProcessVerificationHostCapabilityCatalog.StaticProvider)
    {
    }

    public ProcessVerificationHostCapabilityDescriptor Resolve(ProcessDryRunExecutionNormalizedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return capabilityProvider.Require(
            ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey);
    }
}

internal sealed class ProcessDryRunExecutionSandboxEvaluator(ProcessExecutionCapableDriverFutureGate futureGate)
{
    public ProcessExecutionCapableDriverGateResult Evaluate(ProcessDryRunExecutionNormalizedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return futureGate.Evaluate(
            request.Request.RequestedPolicy,
            request.Request.ApprovalEvidence);
    }
}

internal sealed class ProcessDryRunExecutionAuthorizationEvaluator
{
    public IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> Evaluate(ProcessDryRunExecutionNormalizedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Request.ApprovalEvidence.EffectiveAuthorizationEvidence.MissingGaps();
    }
}

internal sealed class ProcessDryRunExecutionPlanBuilder
{
    public ProcessDryRunExecutionPlan Build(
        ProcessDryRunExecutionNormalizedRequest request,
        ProcessExecutionCapableDriverGateResult gateResult,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> deniedSurfaces,
        IReadOnlyList<ProcessDriverOperation> deniedOperations)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ProcessDryRunExecutionPlan.Create(
            request.Request,
            request.RequestedSurfaces,
            deniedSurfaces,
            deniedOperations,
            gateResult);
    }
}

internal sealed class ProcessDryRunExecutionAuditMapper
{
    public ProcessRuntimeHostContractSnapshot Map(
        ProcessDryRunExecutionNormalizedRequest request,
        ProcessVerificationHostCapabilityDescriptor capability,
        ProcessExecutionCapableDriverGateResult gateResult,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> deniedSurfaces,
        IReadOnlyList<ProcessDriverOperation> deniedOperations,
        IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> authorizationGaps,
        ProcessDryRunExecutionHostDecision hostDecision)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(gateResult);
        ArgumentNullException.ThrowIfNull(deniedSurfaces);
        ArgumentNullException.ThrowIfNull(deniedOperations);
        ArgumentNullException.ThrowIfNull(authorizationGaps);

        var requestedContractSurfaces = request.RequestedSurfaces
            .Select(MapSurface)
            .Distinct()
            .ToArray();
        var deniedContractSurfaces = deniedSurfaces
            .Select(MapSurface)
            .Distinct()
            .ToArray();
        var denials = BuildDenials(
            gateResult,
            deniedContractSurfaces,
            deniedOperations,
            authorizationGaps);

        return ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.DryRunExecution) with
        {
            RequestIdentity = new ProcessRuntimeHostRequestIdentity(
                request.Request.RequestId,
                request.Request.ProcessRunId,
                request.Request.StepRunId,
                request.Request.RequestedBy,
                request.Request.RequestedAt),
            SandboxDecision = new ProcessRuntimeHostSandboxDecision(
                hostDecision == ProcessDryRunExecutionHostDecision.DryRunPlanCreated
                    ? ProcessRuntimeHostSandboxDecisionKind.DryRunPlanAccepted
                    : ProcessRuntimeHostSandboxDecisionKind.Denied,
                executionAllowed: false,
                dryRunOnly: true,
                requestedContractSurfaces,
                deniedContractSurfaces,
                denials),
            AuditReference = new ProcessRuntimeHostAuditReference(
                $"dry-run:{request.Request.RequestId:N}",
                ComputeHash(request, gateResult, deniedSurfaces, deniedOperations, authorizationGaps),
                request.Request.RequestedAt),
            CapabilityDescriptor = new ProcessRuntimeHostCapabilityDescriptorReference(
                capability.Key,
                capability.ContractSurface,
                capability.OperationCategory)
        };
    }

    private static IReadOnlyList<ProcessRuntimeHostDenial> BuildDenials(
        ProcessExecutionCapableDriverGateResult gateResult,
        IReadOnlyList<ProcessRuntimeHostEffectSurface> deniedSurfaces,
        IReadOnlyList<ProcessDriverOperation> deniedOperations,
        IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> authorizationGaps)
    {
        var denials = new List<ProcessRuntimeHostDenial>();
        if (gateResult.MissingRequirements.Count > 0)
        {
            denials.Add(new ProcessRuntimeHostDenial(
                ProcessRuntimeHostDenialCategory.Governance,
                "future-gate-requirements-missing",
                "Execution-capable driver prerequisites are not complete.",
                deniedSurfaces));
        }

        if (authorizationGaps.Count > 0)
        {
            denials.Add(new ProcessRuntimeHostDenial(
                ProcessRuntimeHostDenialCategory.Authorization,
                "authorization-gaps-missing",
                "Approval, revocation, or emergency-stop evidence is incomplete.",
                deniedSurfaces));
        }

        if (deniedOperations.Count > 0 || deniedSurfaces.Count > 0)
        {
            denials.Add(new ProcessRuntimeHostDenial(
                ProcessRuntimeHostDenialCategory.SideEffect,
                "side-effect-denied",
                "Execution-capable side effects are denied by the current runtime-host gate.",
                deniedSurfaces));
        }

        return denials.ToArray();
    }

    private static ProcessRuntimeHostEffectSurface MapSurface(ProcessExecutionCapableDriverSurface surface)
        => surface switch
        {
            ProcessExecutionCapableDriverSurface.CommandExecution => ProcessRuntimeHostEffectSurface.LocalCommand,
            ProcessExecutionCapableDriverSurface.PackageRestore => ProcessRuntimeHostEffectSurface.PackageRestore,
            ProcessExecutionCapableDriverSurface.FileAccess or
                ProcessExecutionCapableDriverSurface.WorkspaceWrite => ProcessRuntimeHostEffectSurface.WorkspaceStorage,
            ProcessExecutionCapableDriverSurface.StorageWrite => ProcessRuntimeHostEffectSurface.ManagedStorage,
            ProcessExecutionCapableDriverSurface.NetworkHttpCall => ProcessRuntimeHostEffectSurface.Network,
            ProcessExecutionCapableDriverSurface.OfficeGraphCall => ProcessRuntimeHostEffectSurface.ExternalService,
            ProcessExecutionCapableDriverSurface.CrmMutation => ProcessRuntimeHostEffectSurface.BusinessRecord,
            ProcessExecutionCapableDriverSurface.ProviderRepair => ProcessRuntimeHostEffectSurface.ProviderRepair,
            ProcessExecutionCapableDriverSurface.FinalizerApplication => ProcessRuntimeHostEffectSurface.Finalizer,
            ProcessExecutionCapableDriverSurface.TransitionMutation => ProcessRuntimeHostEffectSurface.Transition,
            ProcessExecutionCapableDriverSurface.ClaimMutation => ProcessRuntimeHostEffectSurface.Claim,
            ProcessExecutionCapableDriverSurface.RetryScheduling => ProcessRuntimeHostEffectSurface.Retry,
            ProcessExecutionCapableDriverSurface.ProcessMutation => ProcessRuntimeHostEffectSurface.ProcessState,
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unsupported execution-capable surface.")
        };

    private static string ComputeHash(
        ProcessDryRunExecutionNormalizedRequest request,
        ProcessExecutionCapableDriverGateResult gateResult,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> deniedSurfaces,
        IReadOnlyList<ProcessDriverOperation> deniedOperations,
        IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> authorizationGaps)
    {
        var material = string.Join(
            '|',
            request.Request.RequestId,
            request.Request.ProcessRunId,
            request.Request.StepRunId,
            request.Request.RequestedBy,
            request.Request.Purpose,
            gateResult.Decision,
            string.Join(',', deniedSurfaces.Order()),
            string.Join(',', deniedOperations.Order()),
            string.Join(',', authorizationGaps.Order()));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
