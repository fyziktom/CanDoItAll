using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafSavedEffectDispatcher(
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
    IWorkspaceRuntimeServicesFactory workspaceServicesFactory,
    AgentToolAdmissionJournal journal,
    IAgentCatalogReadLeaseStore catalog,
    IReadOnlyList<IAgentRuntimeToolProvider> providers,
    IReadOnlyList<IAgentToolReceiptReconciliationProvider> receiptProviders,
    AgentToolInvocationPolicyPipeline policy,
    AgentToolPolicyCatalog toolPolicies,
    ILogger<MafSavedEffectDispatcher> logger) {

    internal async Task<AgentSavedEffectDispatchResult> DispatchAsync(AgentSavedEffectDispatchRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        var lease = AgentToolRunLease.Current ?? throw Denied("Saved dispatch requires its bound owner-issued lease.");
        if (lease.Session != request.Session) {
            throw Denied("The selected saved effect belongs to another runtime session.");
        }
        var run = await journal.ReadSavedEffectAsync(lease, request.Binding, cancellationToken);
        var selected = AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, request.Binding);
        if (selected.State == AgentToolProposalState.Rejected) {
            return await journal.CheckpointSavedEffectAsync(lease, cancellationToken);
        }
        if (selected.State != AgentToolProposalState.Prepared) {
            throw Denied("This selected effect has already entered execution; use its saved outcome or receipt recovery.");
        }

        var original = selected.Payload;
        var context = await ReadContextAsync(run, lease, cancellationToken);
        var provider = RequireSingle(providers.Where(item => item.SupportsSavedEffect(original)), "runtime tool provider");
        var descriptor = provider.Descriptor ?? throw Denied("The saved-effect provider has no stable ownership descriptor.");
        var purpose = context.InheritedOrigin is null ? AgentRuntimeToolProviderPurpose.InteractiveChat
            : AgentRuntimeToolProviderPurpose.GovernedProcessAutomation;
        if (!descriptor.SupportedPurposes.Contains(purpose) ||
            descriptor.AttachmentPhase != AgentRuntimeToolAttachmentPhase.RuntimeProviders ||
            ToolContractCatalog.WorkspaceToolNames.Contains(original.ToolName, StringComparer.OrdinalIgnoreCase)) {
            throw Denied("Saved dispatch requires a module effect provider supporting its original purpose; workspace tools require their original context protocol.");
        }
        var receiptProvider = RequireSingle(receiptProviders.Where(item => item.Supports(original.ToolName)), "receipt owner");
        var restored = await provider.RestoreSavedEffectAsync(context, original, cancellationToken);
        var metadata = restored.Metadata;
        if (metadata.ProviderKey != descriptor.ProviderKey || metadata.ToolName != original.ToolName ||
            metadata.OperationKind != AgentRuntimeToolOperationKind.Mutation ||
            !toolPolicies.TryResolve(original.ToolName, out var capability) ||
            capability.Classification != ToolInvocationClassification.Mutation) {
            throw Denied("Restoration must retain the registered provider, tool and mutation policy.");
        }
        using var payload = JsonDocument.Parse(original.ArgumentsJson);
        if (payload.RootElement.ValueKind != JsonValueKind.Object || original.SourcePreparation is null ||
            metadata.RestoreAdmission!(payload.RootElement, original.SourcePreparation) != original) {
            throw Denied("Restoration changed the original arguments or source preparation.");
        }
        var arguments = new AIFunctionArguments(payload.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => (object?)item.Value.Clone(), StringComparer.Ordinal));
        await journal.ObserveSavedEffectReceiptAsync(lease, receiptProvider, cancellationToken);

        var pathPolicy = pathPolicyFactory.Create(workspaceRoot);
        await using var workspace = workspaceServicesFactory.Create(WorkspaceExecutionScope.ForRun(
            pathPolicy.RootPath, context.ContextScope, context.Governance, run.Id,
            ExternalTargetRootBindingScope.Resolve(context.Agent), pathPolicy.CaseSensitivity));
        var inspection = new MafScriptPolicyInspectionService(pathPolicy.RootPath, context.ContextScope,
            pathPolicyFactory, workspace.ExternalTargetPathRegistry);
        var claim = await journal.ClaimSavedInvocationAsync(lease, request.Binding, original, cancellationToken);
        using var boundClaim = claim.Bind();
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run, context.ContextScope, workspaceScope);
        using var ownership = AgentRuntimeToolOwnershipContext.BeginScope(new(
            descriptor.ProviderKey, descriptor.DisplayName, original.ToolName));
        using var effects = AgentToolInvocationEffectScope.Begin();
        var outcomeRecorded = false;
        try {
            var result = await InvokeAuthorizedAsync(context, restored, original, arguments, inspection, effects, cancellationToken);
            var checkpoint = MafToolResultCheckpointCodec.Capture(result, effects.PreDispatchFailure, effects.CommittedEffect);
            var effect = effects.CommittedEffect is not null ? AgentToolEffectState.Committed :
                MafRuntimeToolInvocationResultClassifier.Assess(original.ToolName, capability.Classification,
                    result, effects.PreDispatchFailure, effects.RejectedBeforeEffect).EffectState;
            var requiresReconciliation = effect == AgentToolEffectState.Unknown ||
                result is IAgentToolOwnerObservationEvidence { RequiresOwnerReconciliation: true };
            await journal.CompleteInvocationAsync(claim, checkpoint, effect, cancellationToken, requiresReconciliation,
                effects.DisclosureEvidence);
            outcomeRecorded = true;
            var acknowledged = await journal.CheckpointSavedEffectAsync(lease, cancellationToken);
            logger.LogInformation("Saved effect {IntentId} on run {RunId} acknowledged {EffectState}.",
                request.Binding.IntentId.Value, run.Id, acknowledged.EffectState);
            return new(acknowledged.Binding, acknowledged.State, acknowledged.EffectState,
                acknowledged.RunRevision, acknowledged.JournalRevision, (result as IAgentToolOwnerResultEvidence)?.OwnerResult);
        } catch {
            if (!outcomeRecorded) {
                try {
                    await journal.PreserveUncertainInvocationAsync(claim, CancellationToken.None);
                } catch (Exception checkpointFailure) {
                    logger.LogWarning("Could not retain uncertain saved effect {IntentId} on run {RunId}; failure type {FailureType}. Receipt recovery is required.",
                        request.Binding.IntentId.Value, run.Id, checkpointFailure.GetType().Name);
                }
            }
            throw;
        }
    }

    private async Task<AgentRuntimeSavedEffectContext> ReadContextAsync(ExecutionRunRecord run, AgentToolRunLease lease,
        CancellationToken cancellationToken) {
        await using var current = await catalog.AcquireAgentReadLeaseAsync(run.AgentId, cancellationToken);
        if (current.Scope != workspaceScope || current.Agent is not { } agent || agent.Id != run.AgentId) {
            throw Denied("The current Agent catalog does not match this runtime workspace.");
        }
        if (run.ToolAdmission!.PlanningDecision?.InheritedOrigin is { } origin) {
            return AgentRuntimeSavedEffectContext.ForInherited(run.ToolAdmission.Session, agent, origin,
                journal.RequireSavedEffectContextScope(lease));
        }
        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
            ?? throw Denied("The original saved effect has no execution governance.");
        return new(run.ToolAdmission!.Session, agent, governance);
    }

    private async ValueTask<object?> InvokeAuthorizedAsync(AgentRuntimeSavedEffectContext context,
        AgentRuntimeSavedEffectTool tool, AgentToolPreparedPayload original, AIFunctionArguments arguments,
        MafScriptPolicyInspectionService inspection, AgentToolInvocationEffectScope effects, CancellationToken cancellationToken) {
        var entered = false;
        try {
            if (!context.Agent.Permissions.CanUseTools) {
                throw Denied("Current Agent permissions prohibit tool execution.");
            }
            var invocationArguments = arguments.ToArray();
            var neutral = MafToolInvocationPolicyContextFactory.Create(context.Agent, original.ToolName, invocationArguments,
                ToolInvocationClassification.Mutation, toolPolicies, isKnownTool: true,
                new(Automatic: false, WrapperAvailable: false, WrapperEffectiveForProvider: false, ApplicationAvailable: true),
                context.Governance, inspection.ResolveScriptContentInspectionForPolicy, WorkspaceExecutionAuditContext.Current, []);
            var evaluation = await policy.ComposeAndEvaluateAsync(neutral, WorkspaceExecutionAuditContext.Current, cancellationToken);
            AgentToolPolicyBlockGuard.ThrowIfBlocked(original.ToolName, evaluation.Decision, evaluation.EffectiveContext.HasEffectiveApprovalPath);
            if (MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(tool.Function, invocationArguments, out var failure)) {
                AgentToolInvocationEffectScope.RecordPreDispatchFailure(new(failure.ErrorCode, failure.Message) {
                    CanRetryWithCorrectedInput = failure.CanRetryWithCorrectedInput
                });
                return failure;
            }
            await using var authorization = await tool.Metadata.AuthorizeAdmissionAsync!(original, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            entered = true;
            try {
                return await tool.Function.InvokeAsync(arguments, cancellationToken);
            } catch (Exception exception) when (effects.CommittedEffect is null &&
                MafAgentToolFailureMapper.TryMap(exception, out var ownerFailure) &&
                ownerFailure.EffectState is AgentToolEffectState.NotCommitted or AgentToolEffectState.None) {
                return ownerFailure;
            }
        } catch (Exception exception) when (!entered && effects.CommittedEffect is null &&
            exception is AgentToolAdmissionException or AgentToolPolicyBlockedException or UnauthorizedAccessException) {
            var failure = new AgentToolFailureResult(false, "ToolPolicyDenied", "Current execution authority denied this saved proposal.", false) {
                EffectState = AgentToolEffectState.NotCommitted
            };
            AgentToolInvocationEffectScope.RecordPreDispatchFailure(new(failure.ErrorCode, failure.Message));
            return failure;
        }
    }

    private static T RequireSingle<T>(IEnumerable<T> matching, string owner) {
        using var iterator = matching.GetEnumerator();
        if (!iterator.MoveNext()) {
            throw Denied($"No registered {owner} can restore this original saved effect.");
        }
        var selected = iterator.Current;
        if (iterator.MoveNext()) {
            throw Denied($"More than one registered {owner} claims this original saved effect.");
        }
        return selected;
    }

    private static AgentToolAdmissionException Denied(string message) => new("tool-admission.saved-runtime-denied", message);
}
