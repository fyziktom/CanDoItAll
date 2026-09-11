using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessAssetToolAdmission(IAgentToolAdmissionVerifier admissions, ICanonicalRuntimeDatabase database,
    IProcessExecutionDispatchAuthorityReader executions, ProjectProcessLaunchAuthorityService authorities,
    ProjectWorkbenchService workbench, ProjectProcessAssetProposalCodec codec) : IAgentToolReceiptReconciliationProvider {
    public const string EffectSourceKind = "process-native-asset-contribution";

    public static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation &&
            context.AdmittedToolSession?.BackgroundSource is not null && context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable;

    public bool Supports(string toolName) => codec.Supports(toolName);

    public async ValueTask<IAsyncDisposable> AuthorizeProposalAsync(AgentRuntimeToolProviderContext context,
        AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        var input = codec.Read(payload);
        var session = await RequireSessionAsync(context, cancellationToken);
        var execution = await ReadExecutionAsync(session, cancellationToken);
        if (execution.ProjectId != input.ProjectId) {
            throw Denied("The asset proposal cannot change the original Process project.");
        }
        return CompletedCheck.Instance;
    }

    internal Task<ProjectProcessAssetInvocation> RequireInvocationAsync(AgentRuntimeToolProviderContext context,
        ProjectProcessAssetCreateProposal input, string parentNodeKey, CancellationToken cancellationToken)
        => RequireInvocationAsync(context, codec.Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
            JsonSerializer.SerializeToElement(input, ProjectProcessAssetProposalCodec.Json)), parentNodeKey, cancellationToken);

    internal Task<ProjectProcessAssetInvocation> RequireInvocationAsync(AgentRuntimeToolProviderContext context,
        ProjectProcessAssetRevisionProposal input, CancellationToken cancellationToken)
        => RequireInvocationAsync(context, codec.Prepare(ProjectProcessAssetProposalCodec.RevisionToolName,
            JsonSerializer.SerializeToElement(input, ProjectProcessAssetProposalCodec.Json)), input.NodeId, cancellationToken);

    private async Task<ProjectProcessAssetInvocation> RequireInvocationAsync(AgentRuntimeToolProviderContext context,
        AgentToolPreparedPayload payload, string parentNodeKey, CancellationToken cancellationToken) {
        var input = codec.Read(payload);
        var session = await RequireSessionAsync(context, cancellationToken);
        var admitted = await admissions.RequireInvocationAsync(session.Reference, payload.ToolName, payload.Digest, cancellationToken);
        if (admitted.Session != session.Reference || admitted.Payload != payload || admitted.IntentId.Value == Guid.Empty ||
                admitted.BatchId.Value == Guid.Empty || admitted.ApprovalStatus != ExecutionApprovalStatus.Approved || admitted.ApprovedDigest != payload.Digest) {
            throw Denied("The current serial dispatch does not own this exact approved asset proposal.");
        }
        var saved = await workbench.FindProcessAssetAsync(admitted.IntentId, cancellationToken);
        if (saved is not null) {
            RequireOriginalProposal(saved, session.Reference, admitted.IntentId, payload);
            await using var read = await authorities.AcquireResultReadAsync(saved.Plan.Execution.SourceAuthority!, cancellationToken);
            return new(admitted, saved.Plan.Execution, saved.Plan.ParentNodeKey);
        }
        var execution = await ReadExecutionAsync(session, cancellationToken);
        if (execution.ProjectId != input.ProjectId || !execution.ObservedCurrentDispatch || execution.SourceAuthority?.CanCreateAssets != true ||
                !execution.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw Denied("The original Process source does not permit this new asset effect.");
        }
        await authorities.RequireCurrentAsync(execution.SourceAuthority, cancellationToken);
        return new(admitted, execution, parentNodeKey);
    }

    public async ValueTask<IAsyncDisposable?> AuthorizeResultDisclosureAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var input = codec.Read(disclosure.Payload);
        var session = await RequireSessionAsync(context, cancellationToken);
        var saved = await workbench.FindProcessAssetAsync(disclosure.IntentId, cancellationToken);
        if (saved is null && disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            var execution = await ReadExecutionAsync(session, cancellationToken);
            if (execution.ProjectId != input.ProjectId || execution.SourceAuthority is null) {
                throw Denied("The uncommitted asset result has no original project authority.");
            }
            return await authorities.AcquireResultReadAsync(execution.SourceAuthority, cancellationToken);
        }
        if (saved is null) {
            throw Denied("The saved asset result has no original owner preparation.");
        }
        RequireOriginalProposal(saved, session.Reference, disclosure.IntentId, disclosure.Payload);
        if (disclosure.EffectState != AgentToolEffectState.NotCommitted) {
            RequireResultIdentity(disclosure, saved);
        }
        return await authorities.AcquireResultReadAsync(saved.Plan.Execution.SourceAuthority!, cancellationToken);
    }

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        codec.Read(admitted.Payload);
        if (admitted.Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                admitted.Session.Profile.ProfileId != database.Profile.Profile.Id || admitted.Session.Reference.BackgroundSource is null) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var saved = await workbench.FindProcessAssetAsync(admitted.Binding.IntentId, cancellationToken);
        await claim.RequireAsync(cancellationToken);
        if (saved is null) {
            return AgentToolReceiptObservation.NotObserved;
        }
        RequireOriginalProposal(saved, admitted.Session.Reference, admitted.Binding.IntentId, admitted.Payload);
        await using var read = await authorities.AcquireResultReadAsync(saved.Plan.Execution.SourceAuthority!, cancellationToken);
        return saved.Receipt is null ? AgentToolReceiptObservation.NotObserved : new(
            new(EffectSourceKind, saved.Receipt.IntentId.Value.ToString("D")),
            AgentToolProtocolEnvelope.Create("process-native-asset-receipt", 1,
                JsonSerializer.Serialize(saved.Receipt, ProjectProcessAssetPersistence.Json)));
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        if (!UsesJournal(context) || context.Governance is not null) {
            throw Denied("The asset producer requires its original background Process journal.");
        }
        var session = await admissions.RequireSessionAsync(context.AdmittedToolSession!, cancellationToken);
        if (session.Reference != context.AdmittedToolSession || session.AgentId != context.Agent.Id ||
                session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation || session.Profile.ProfileId != database.Profile.Profile.Id) {
            throw Denied("The asset producer does not match its saved Process executor, session or runtime profile.");
        }
        return session;
    }

    private async Task<ProcessExecutionDispatchAuthority> ReadExecutionAsync(AgentToolSessionAdmission session, CancellationToken cancellationToken) {
        var source = session.Reference.BackgroundSource!;
        var result = await executions.ReadAsync(session.Reference.ExecutionRunId, cancellationToken);
        if (result.Snapshot is not { } execution || execution.Evidence.ExecutionRunId != session.Reference.ExecutionRunId ||
                execution.Evidence.ExecutorAgentId != session.AgentId || source.SourceId != execution.Evidence.StepKey ||
                source.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(execution.OwnerFingerprint)) {
            throw Denied("The asset proposal no longer matches its saved Process execution occurrence.");
        }
        return execution;
    }

    private static void RequireOriginalProposal(ProjectProcessAssetSnapshot saved, AgentToolSessionReference session,
        AgentToolBusinessIntentId intentId, AgentToolPreparedPayload payload) {
        RetainedEvidenceImport.RequireNative(saved.ImportedHistory);
        var producer = saved.Plan.Producer;
        if (producer.Session != session || producer.IntentId != intentId || producer.Payload != payload) {
            throw Denied("The saved native asset belongs to a different source or dynamic proposal occurrence.");
        }
    }

    private static void RequireResultIdentity(AgentToolResultDisclosure disclosure, ProjectProcessAssetSnapshot saved) {
        ProjectProcessAssetReceiptObservation? observed;
        string? nodeId;
        if (disclosure.Payload.ToolName == ProjectProcessAssetProposalCodec.RevisionToolName) {
            var result = disclosure.Result.Deserialize<ProjectStructureAssetDescriptor>(ProjectProcessAssetPersistence.Json);
            observed = result?.ProcessAssetReceipt;
            nodeId = result?.NodeId;
            if (result?.ProjectId != saved.Plan.ProjectAdmission.ProjectId) {
                throw Denied("The saved asset revision result changed its original project.");
            }
        } else {
            var result = disclosure.Result.Deserialize<ProjectStructureNodeSummary>(ProjectProcessAssetPersistence.Json);
            observed = result?.ProcessAssetReceipt;
            nodeId = result?.Id;
        }
        if (saved.Receipt is null || observed?.Receipt != saved.Receipt || nodeId != saved.Receipt.NodeId) {
            throw Denied("The saved asset result does not match its atomic native receipt.");
        }
    }

    private static ProjectStructureAgentException Denied(string message) => new(403, "ProcessAssetAdmissionDenied", message);

    private sealed class CompletedCheck : IAsyncDisposable {
        internal static CompletedCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
