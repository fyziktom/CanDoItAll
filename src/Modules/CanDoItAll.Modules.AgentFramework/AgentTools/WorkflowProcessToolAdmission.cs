using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowProcessToolAdmission(IAgentToolAdmissionVerifier admissions,
    IWorkflowProcessToolSourceAuthority source, WorkflowProcessToolProposalCodec codec,
    IDbContextFactory<WorkflowDbContext> database) : IAgentToolReceiptReconciliationProvider {
    public const string EffectSourceKind = "workflow-process-tool-admission";

    public static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation &&
            context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable && context.AdmittedToolSession?.BackgroundSource is not null;

    public bool Supports(string toolName) => toolName == WorkflowToolPolicy.WorkflowsRunStart;

    public AgentRuntimeToolMetadata BindMetadata(AgentRuntimeToolProviderContext context, AgentRuntimeToolMetadata metadata)
        => metadata with {
            PrepareAdmission = codec.Prepare,
            AuthorizeAdmissionAsync = async (payload, token) => {
                codec.Read(payload);
                await RequireSessionAsync(context, token);
                return CompletedCheck.Instance;
            },
            AuthorizeResultDisclosureAsync = (disclosure, token) => AuthorizeResultDisclosureAsync(context, disclosure, token)
        };

    public async Task<WorkflowLaunchOrigin.ProcessToolInvocation> CaptureAsync(AgentRuntimeToolProviderContext context,
        WorkflowAgentStartInput request, CancellationToken cancellationToken) {
        var payload = codec.Prepare(request);
        var session = await RequireSessionAsync(context, cancellationToken);
        var invocation = await admissions.RequireInvocationAsync(session.Reference, payload.ToolName, payload.Digest, cancellationToken);
        if (invocation.Payload != payload) {
            throw new InvalidOperationException("The Workflow request differs from the active approved Process proposal.");
        }
        var saved = await FindAsync(new(invocation.IntentId.Value), cancellationToken);
        if (saved is not null) {
            var original = RequireReceipt(saved, session, invocation.IntentId, invocation.BatchId, payload);
            await using var read = await source.AcquireDisclosureAsync(original, cancellationToken);
            return original;
        }
        var capability = context.Agent.Capabilities.Single(item => item.Kind == CapabilityKind.Tool &&
            item.CapabilityKey == WorkflowRuntimeCapabilityKeys.RunStart);
        return await source.CaptureAsync(session, invocation, capability.CapabilityId, cancellationToken);
    }

    public async Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken) {
        var origin = run.Origin as WorkflowLaunchOrigin.ProcessToolInvocation
            ?? throw new InvalidOperationException("A Process Workflow admission requires its typed original source.");
        var binding = origin.Invocation;
        var session = await admissions.RequireSessionAsync(binding.Session, cancellationToken);
        var invocation = await admissions.RequireInvocationAsync(binding.Session, binding.ToolName, binding.ProposalFingerprint, cancellationToken);
        RequireReceipt(run, session, invocation.IntentId, invocation.BatchId, invocation.Payload);
        return await source.AcquireForMutationAsync(origin, cancellationToken);
    }

    public static void RecordCommitted(WorkflowLaunchResult result, WorkflowLaunchOrigin.ProcessToolInvocation origin) {
        if (result.Run.RunId != origin.Invocation.PreparedRunId || result.Run.Origin is not WorkflowLaunchOrigin.ProcessToolInvocation saved ||
                saved.Invocation != origin.Invocation) {
            throw new InvalidOperationException("The Workflow owner acknowledgement does not match the original Process proposal.");
        }
        AgentToolInvocationEffectScope.RecordCommitted(EffectSourceKind, result.Run.RunId.Value.ToString("D"));
    }

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        codec.Read(admitted.Payload);
        var run = await FindAsync(new(admitted.Binding.IntentId.Value), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        if (run is null) {
            return AgentToolReceiptObservation.NotObserved;
        }
        var origin = RequireReceipt(run, admitted.Session, admitted.Binding.IntentId, admitted.Binding.BatchId, admitted.Payload);
        await using var held = await source.AcquireDisclosureAsync(origin, cancellationToken);
        return new(new(EffectSourceKind, run.RunId.Value.ToString("D")),
            AgentToolProtocolEnvelope.Create("workflow-process-tool-receipt", 1, JsonSerializer.Serialize(new {
                RunId = run.RunId.Value,
                WorkflowId = run.WorkflowId.Value,
                VersionId = run.VersionId.Value,
                run.State
            }, WorkflowProcessToolProposalCodec.SerializerOptions)));
    }

    private async ValueTask<IAsyncDisposable?> AuthorizeResultDisclosureAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        codec.Read(disclosure.Payload);
        var session = await RequireSessionAsync(context, cancellationToken);
        if (disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            return CompletedCheck.Instance;
        }
        var result = disclosure.Result.Deserialize<WorkflowAgentStartResult>(WorkflowProcessToolProposalCodec.SerializerOptions)
            ?? throw new AgentToolReceiptAccessDeniedException();
        var run = await FindAsync(new(disclosure.IntentId.Value), cancellationToken)
            ?? throw new AgentToolReceiptAccessDeniedException();
        if (result.Run.RunId != run.RunId.Value || result.Run.WorkflowId != run.WorkflowId.Value || result.Run.VersionId != run.VersionId.Value) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        if (run.Origin is not WorkflowLaunchOrigin.ProcessToolInvocation saved) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var origin = RequireReceipt(run, session, disclosure.IntentId, saved.Invocation.BatchId, disclosure.Payload);
        return await source.AcquireDisclosureAsync(origin, cancellationToken);
    }

    private WorkflowLaunchOrigin.ProcessToolInvocation RequireReceipt(WorkflowRunSnapshot run, AgentToolSessionAdmission session,
        AgentToolBusinessIntentId intentId, AgentToolBatchId batchId, AgentToolPreparedPayload payload) {
        var request = codec.Read(payload);
        if (session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation || session.Reference.BackgroundSource is null ||
                run.Origin is not WorkflowLaunchOrigin.ProcessToolInvocation origin ||
                origin.Invocation.Session != session.Reference || origin.Invocation.Profile != session.Profile ||
                origin.Invocation.ExecutorAgentId != session.AgentId || origin.Invocation.IntentId != intentId || origin.Invocation.BatchId != batchId ||
                origin.Invocation.ToolName != payload.ToolName || origin.Invocation.ProposalFingerprint != payload.Digest ||
                run.RunId != origin.Invocation.PreparedRunId || run.WorkflowId.Value != request.WorkflowId ||
                request.VersionId is { } version && run.VersionId.Value != version) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return origin;
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        if (!UsesJournal(context)) {
            throw new InvalidOperationException("A Process Workflow tool requires its active background journal session.");
        }
        var session = await admissions.RequireSessionAsync(context.AdmittedToolSession!, cancellationToken);
        if (session.AgentId != context.Agent.Id || session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return session;
    }

    private async Task<WorkflowRunSnapshot?> FindAsync(WorkflowRunId runId, CancellationToken cancellationToken) {
        await using var context = await database.CreateDbContextAsync(cancellationToken);
        var row = await context.Set<WorkflowRunRecordEntity>().AsNoTracking().SingleOrDefaultAsync(item => item.RunId == runId.Value, cancellationToken);
        return row?.ToSnapshot();
    }

    private sealed class CompletedCheck : IAsyncDisposable {
        public static CompletedCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
