using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    public async Task<WorkflowLaunchOrigin.ProcessToolInvocation> CaptureAsync(AgentToolSessionAdmission session,
        AgentToolAdmittedInvocation invocation, Guid launchCapabilityId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        var verifier = RequireToolAdmissions();
        var current = await verifier.RequireSessionAsync(session.Reference, cancellationToken);
        var admitted = await verifier.RequireInvocationAsync(session.Reference, invocation.Payload.ToolName,
            invocation.Payload.Digest, cancellationToken);
        if (current != session || admitted != invocation || invocation.Session != session.Reference ||
                invocation.ApprovalStatus != ExecutionApprovalStatus.Approved || invocation.ApprovedDigest != invocation.Payload.Digest ||
                session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation) {
            throw Denied("The Workflow launch does not match the active approved Process proposal.");
        }
        var dispatch = await ReadToolDispatchAsync(session.Reference.ExecutionRunId, cancellationToken);
        var binding = new WorkflowProcessToolBinding(session.Reference, session.Profile, invocation.BatchId, invocation.IntentId,
            invocation.Payload.ToolName, invocation.Payload.Digest, session.AgentId, launchCapabilityId,
            new(dispatch.Evidence.RunId.Value), new(dispatch.Evidence.StepInstanceId.Value), dispatch.ReadinessHash);
        RequireProcessToolBinding(binding, dispatch);
        if (!dispatch.ObservedCurrentDispatch || !dispatch.AllowedOperations.Contains(ProcessOperationContractNames.LaunchRuntime,
                StringComparer.OrdinalIgnoreCase)) {
            throw Denied("The original Process claim no longer permits launching a Workflow.");
        }
        return new(binding, new(dispatch.Evidence.RunId.Value.ToString("D"))) {
            AuthorizationScope = WorkspaceScopeDescriptor.Process(dispatch.Evidence.RunId.Value.ToString("D")),
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            StructureAuthority = dispatch.SourceAuthority is null ? null : CaptureProcess(dispatch,
                dispatch.SourceAuthority.ProjectAdmission?.ProjectId ?? Guid.Empty, binding)
        };
    }

    public async Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowLaunchOrigin.ProcessToolInvocation origin,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(origin);
        var binding = origin.Invocation;
        var verifier = RequireToolAdmissions();
        var session = await verifier.RequireSessionAsync(binding.Session, cancellationToken);
        var invocation = await verifier.RequireInvocationAsync(binding.Session, binding.ToolName, binding.ProposalFingerprint, cancellationToken);
        if (session.AgentId != binding.ExecutorAgentId || session.Profile != binding.Profile ||
                session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                invocation.Session != binding.Session || invocation.BatchId != binding.BatchId || invocation.IntentId != binding.IntentId ||
                invocation.ApprovalStatus != ExecutionApprovalStatus.Approved || invocation.ApprovedDigest != binding.ProposalFingerprint) {
            throw Denied("The persisted Workflow admission differs from the active Process proposal.");
        }
        return await AcquireProcessToolAsync(origin, disclosure: false, cancellationToken);
    }

    public async Task<IAsyncDisposable> AcquireDisclosureAsync(WorkflowLaunchOrigin.ProcessToolInvocation origin,
        CancellationToken cancellationToken = default)
        => await AcquireProcessToolAsync(origin, disclosure: true, cancellationToken);

    private IAgentToolAdmissionVerifier RequireToolAdmissions() => toolAdmissions
        ?? throw new InvalidOperationException("Process Workflow tools require the active journal admission verifier.");

    private async Task<ProcessExecutionDispatchAuthority> ReadToolDispatchAsync(Guid executionId, CancellationToken cancellationToken) {
        var observed = await (processReader ?? throw new InvalidOperationException("Process Workflow tools require their saved execution authority reader."))
            .ReadAsync(executionId, cancellationToken);
        return observed.Snapshot ?? throw Denied("The original Process execution evidence is unavailable; reconciliation is required.");
    }

    private async Task<IWorkflowStructureSourceAuthorityLease> AcquireProcessToolAsync(WorkflowLaunchOrigin.ProcessToolInvocation origin,
        bool disclosure, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(origin);
        var binding = origin.Invocation;
        var dispatch = await ReadToolDispatchAsync(binding.Session.ExecutionRunId, cancellationToken);
        RequireProcessToolBinding(binding, dispatch);
        if (origin.AuthorizationScope != WorkspaceScopeDescriptor.Process(binding.ProcessRun.Value.ToString("D")) ||
                origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint ||
                !disclosure && (!dispatch.ObservedCurrentDispatch || !dispatch.AllowedOperations.Contains(
                    ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase))) {
            throw Denied("The original Process source no longer permits this Workflow admission.");
        }
        if (dispatch.SourceAuthority is not null) {
            if (origin.StructureAuthority is not { ProjectScope: not null, ProcessAuthority.ToolInvocation: { } saved } authority || saved != binding) {
                throw Denied("The Workflow lost its original Process source ceiling.");
            }
            return await AcquireAsync(authority, disclosure ? WorkflowStructureAuthorityUse.Disclosure : WorkflowStructureAuthorityUse.Admission,
                cancellationToken: cancellationToken);
        }
        if (origin.StructureAuthority is not null) {
            throw Denied("This Process execution has no saved project authority and cannot acquire one through a Workflow.");
        }
        var held = await RequireCatalog().AcquireAgentsReadLeaseAsync([binding.ExecutorAgentId], cancellationToken);
        try {
            RequireProcessToolExecutor(binding, held, disclosure);
            return new ProcessToolSourceLease(this, binding, dispatch, held, disclosure);
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private void RequireProcessToolBinding(WorkflowProcessToolBinding binding, ProcessExecutionDispatchAuthority dispatch) {
        binding.Validate();
        var background = binding.Session.BackgroundSource!;
        if (binding.Profile.ProfileId != canonicalDatabase.Profile.Profile.Id ||
                binding.Profile.Fingerprint != canonicalDatabase.Profile.Profile.Runtime.Fingerprint ||
                binding.Profile.Generation != (generations ?? throw new InvalidOperationException("Process Workflow tools require the current profile generation.")).GetGeneration() ||
                dispatch.Evidence.ExecutionRunId != binding.Session.ExecutionRunId || dispatch.Evidence.ExecutorAgentId != binding.ExecutorAgentId ||
                dispatch.Evidence.RunId.Value != binding.ProcessRun.Value || dispatch.Evidence.StepInstanceId.Value != binding.StepInstance.Value ||
                dispatch.ReadinessHash != binding.ReadinessHash || background.SourceId != dispatch.Evidence.StepKey ||
                background.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(dispatch.OwnerFingerprint)) {
            throw Denied("The Workflow proposal's original profile, executor or Process claim does not match its saved execution.");
        }
    }

    private void RequireProcessToolExecutor(WorkflowProcessToolBinding binding, IAgentCatalogReadLease? held, bool disclosure) {
        var executor = held?.Agents.SingleOrDefault(agent => agent.Id == binding.ExecutorAgentId);
        var key = disclosure ? WorkflowRuntimeCapabilityKeys.RunStatusGet : WorkflowRuntimeCapabilityKeys.RunStart;
        if (held is null || held.Scope != WorkspaceScopeDescriptor.Organization(binding.Profile.ProfileId.ToString("N")) ||
                executor is null || executor.IsTemplate || executor.Status != AgentLifecycleStatus.Active || !executor.Permissions.CanUseTools) {
            throw Denied("The assigned Process executor is no longer available for this Workflow operation.");
        }
        var assignments = executor.Capabilities.Where(item => item.CapabilityKey == key).ToArray();
        if (assignments.Length != 1 || assignments[0].Kind != CapabilityKind.Tool || !disclosure && assignments[0].CapabilityId != binding.LaunchCapabilityId ||
                held.Capabilities.Count(item => item.Id == assignments[0].CapabilityId && item.Key == key && item.Kind == CapabilityKind.Tool) != 1) {
            throw Denied("The assigned Process executor's current Workflow capability no longer permits this operation.");
        }
    }

    private Task RequireProcessToolMutationAsync(ProcessExecutionDispatchAuthority dispatch, CancellationToken cancellationToken)
        => (processGuard ?? throw new InvalidOperationException("Process Workflow admission requires the actual Process owner transaction guard."))
            .RequireDispatchAsync(dispatch, cancellationToken);

    private sealed class ProcessToolSourceLease(ProjectStructureWorkflowAuthorityService owner, WorkflowProcessToolBinding binding,
        ProcessExecutionDispatchAuthority dispatch, IAgentCatalogReadLease held, bool disclosure) : IWorkflowStructureSourceAuthorityLease {
        private int disposed;

        public async Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (disclosure) {
                throw new InvalidOperationException("Workflow receipt disclosure cannot authorize a new admission.");
            }
            owner.RequireProcessToolExecutor(binding, held, disclosure: false);
            await owner.RequireProcessToolMutationAsync(dispatch, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0) {
                await held.DisposeAsync();
            }
        }
    }
}
