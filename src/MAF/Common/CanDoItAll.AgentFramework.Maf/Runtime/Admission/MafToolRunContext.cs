using CanDoItAll.AgentFramework.Runtime.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafToolRunContext {
    private static readonly AsyncLocal<MafToolRunContext?> Ambient = new();
    private readonly AgentToolAdmissionJournal journal;
    private readonly AgentToolPolicyCatalog toolPolicies;
    private readonly AgentToolRunLease lease;
    private readonly IReadOnlyDictionary<string, AgentRuntimeToolMetadata> metadata;
    private readonly Dictionary<string, AITool> tools;
    private readonly MafContextToolRegistration[] contextToolRegistrations;
    private readonly IReadOnlyList<AITool> nativeTools;
    private readonly HashSet<AgentToolBusinessIntentId> completedInCurrentInvocation = [];
    private int responseCursor;
    private AgentToolBatchId? dispatchBatch;
    private AgentToolJournalRecord? recoveryState;

    private MafToolRunContext(AgentToolAdmissionJournal journal, AgentToolRunLease lease,
        AgentToolInvocationSegment segment, RuntimeCapabilityState? capabilities) {
        this.journal = journal;
        toolPolicies = capabilities?.ToolPolicies ?? AgentToolPolicyCatalog.BuiltIn;
        this.lease = lease;
        Segment = segment;
        responseCursor = segment.FirstBatchOrdinal;
        metadata = (capabilities?.RuntimeToolMetadata ?? []).ToDictionary(item => item.ToolName, StringComparer.Ordinal);
        tools = (capabilities?.Tools ?? []).Where(tool => tool is AIFunction).ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        contextToolRegistrations = (capabilities?.ContextToolRegistrations ?? []).ToArray();
        var contextNames = contextToolRegistrations.SelectMany(registration => registration.ToolNames).ToArray();
        if (contextNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != contextNames.Length ||
                contextNames.Any(name => tools.Keys.Contains(name, StringComparer.OrdinalIgnoreCase))) {
            throw Denied("Context tool registrations must not shadow another composed tool.");
        }
        nativeTools = (capabilities?.Tools ?? []).Where(tool => tool is not AIFunctionDeclaration).ToArray();
    }

    internal static MafToolRunContext? Current => Ambient.Value;
    internal AgentToolInvocationSegment Segment { get; }
    internal bool HasActiveProviderDispatch { get; private set; }

    internal IDisposable Bind() {
        var previous = Ambient.Value;
        Ambient.Value = this;
        return new RestoreScope(previous);
    }

    internal static async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
        AgentToolAdmissionJournal journal, AgentToolRunLease lease, AIAgent runtimeAgent, AgentSession session,
        AgentDefinition agent, ProviderProfile provider, string model, ChatSessionRecord chat,
        AgentRuntimeExecutionOptions options, RuntimeCapabilityState? capabilities, List<ChatMessage> input,
        IMafRuntimeSessionPersistenceDriver persistence, bool isApprovalContinuation,
        Func<ExecutionState, string, string, Task> progress, CancellationToken cancellationToken) {
        var saved = await journal.ReadAsync(lease, cancellationToken);
        var latest = saved.Segments.LastOrDefault();
        Guid? continuesSegmentId = null;
        if (latest?.ApprovalCheckpoint is not null) {
            var pending = latest.PendingApprovals;
            var decisions = pending.Select(approval => {
                var proposal = AgentToolJournalTransitions.RequireProposal(saved, approval.ToolAdmission
                    ?? throw Denied("The saved SDK approval has no immutable intent binding."));
                if (proposal.ApprovalStatus == ExecutionApprovalStatus.Pending) {
                    throw new AgentToolAdmissionException("tool-admission.approval-pending",
                        "The saved SDK proposal still requires its exact approval decision.");
                }

                return new AgentRuntimeApprovalDecision(approval.ApprovalId,
                    proposal.ApprovalStatus == ExecutionApprovalStatus.Approved);
            }).ToArray();
            chat = chat with { Compatibility = ChatSessionRuntimeCompatibilityRecord.Create(
                chat.Compatibility?.RuntimeSessionKey, MafToolProtocolCodec.Decode<string>(latest.ApprovalCheckpoint),
                pending, autoApprovePendingToolCalls: false) };
            var evaluation = MafRuntimeSessionBuilder.ResolveRestoreEvaluation(agent, provider, model, chat, options,
                isApprovalContinuation: true);
            if (!evaluation.ShouldRestore) {
                throw Denied("The saved approval SDK checkpoint is incompatible with current execution authority.");
            }

            session = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(runtimeAgent, agent, provider, model,
                chat, options, cancellationToken, isApprovalContinuation: true, progress);
            input = new MafApprovalContinuationDriver().CreateApprovalInputMessages(chat, decisions).ToList();
            RestoreNativeApprovalInputs(saved, input);
            isApprovalContinuation = true;
            continuesSegmentId = latest.Id;
        }

        AgentToolInvocationSegment segment;
        if (latest is not null && latest.ApprovalCheckpoint is null) {
            segment = latest;
            var checkpoint = MafToolProtocolCodec.Decode<RestartCheckpoint>(segment.RestartCheckpoint);
            var restartChat = chat with { Compatibility = ChatSessionRuntimeCompatibilityRecord.Create(
                chat.Compatibility?.RuntimeSessionKey, checkpoint.SerializedSessionStateJson,
                checkpoint.PendingApprovals, autoApprovePendingToolCalls: false) };
            var evaluation = MafRuntimeSessionBuilder.ResolveRestoreEvaluation(agent, provider, model, restartChat,
                options, checkpoint.IsApprovalContinuation);
            if (!evaluation.ShouldRestore) {
                throw Denied("The saved SDK checkpoint is incompatible with current provider, context or authority policy.");
            }

            session = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(runtimeAgent, agent, provider, model,
                restartChat, options, cancellationToken, checkpoint.IsApprovalContinuation, progress);
            input = checkpoint.Input;
        } else {
            var serialized = await persistence.TrySerializePersistableRuntimeSessionAsync(runtimeAgent, session,
                provider, model, options, chat.Compatibility?.PendingApprovals ?? [],
                (state, phase, message) => progress(state == ExecutionState.Persisting ? ExecutionState.Preparing : state, phase, message),
                cancellationToken)
                ?? throw Denied("This invocation has no persistable SDK checkpoint; admitted tools cannot execute.");
            var checkpoint = new RestartCheckpoint(serialized, input, isApprovalContinuation,
                chat.Compatibility?.PendingApprovals.ToArray() ?? []);
            var envelope = MafToolProtocolCodec.Encode(checkpoint);
            saved = await journal.BeginSegmentAsync(lease, envelope, continuesSegmentId, cancellationToken);
            segment = saved.Segments[^1];
        }

        var context = new MafToolRunContext(journal, lease, segment, capabilities);
        await context.CheckRecoveryAsync(cancellationToken);
        return (context, session, input);
    }

    internal void RegisterContextTools(MafContextToolRegistration registration, IEnumerable<AITool> supplied) {
        if (!contextToolRegistrations.Contains(registration)) {
            throw Denied("The context provider is not part of this admitted runtime.");
        }
        var functions = registration.RequireTools(supplied);
        foreach (var name in registration.ToolNames) {
            tools.Remove(name);
        }
        foreach (var function in functions) {
            tools.Add(function.Name, function);
        }
    }

    internal AgentToolPreparedPayload Prepare(string name, IDictionary<string, object?>? arguments,
        bool allowDeferredContextTool = false, AgentToolProtocolEnvelope? persistedSource = null) {
        if (!tools.ContainsKey(name) && !(allowDeferredContextTool && contextToolRegistrations.Any(registration =>
                registration.ToolNames.Contains(name, StringComparer.Ordinal)))) {
            throw Denied("The proposed tool is outside the current admitted toolset.");
        }

        var element = JsonSerializer.SerializeToElement(arguments ?? new Dictionary<string, object?>(), MafToolProtocolCodec.SerializationOptions);
        if (metadata.TryGetValue(name, out var descriptor) && descriptor.PrepareAdmission is { } prepare) {
            if (descriptor.AuthorizeAdmissionAsync is null) {
                throw Denied("An admission-aware provider must supply its current authorization boundary.");
            }

            return prepare(element);
        }

        var classification = toolPolicies.Classify(name);
        var json = MafToolProtocolCodec.Canonicalize(element);
        var effect = classification == ToolInvocationClassification.Read ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation;
        var recovery = effect == AgentToolProposalEffect.Read &&
                descriptor?.RecoveryPolicy != AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry
            ? AgentToolProposalRecovery.RevalidateAndRead
            : AgentToolProposalRecovery.ReconcileBeforeRetry;
        var original = new AgentToolPreparedPayload(name, 1, MafToolProtocolCodec.Digest(new { Name = name, Arguments = element }), json, effect,
            recovery);
        var preparation = contextToolRegistrations.SingleOrDefault(registration =>
            registration.ToolNames.Contains(name, StringComparer.Ordinal))?.SourcePreparation;
        if (preparation is null && persistedSource is not null) {
            throw MafContextToolSourceContract.MissingSource();
        }
        var source = preparation is null ? null : allowDeferredContextTool
            ? persistedSource ?? throw MafContextToolSourceContract.MissingSource()
            : preparation.Prepare(name, element);
        return MafContextToolSourceContract.Bind(original, source);
    }

    private AgentToolPreparedPayload PrepareInvocation(FunctionCallContent call) {
        if (recoveryState is not { } saved || !contextToolRegistrations.Any(registration =>
                registration.ToolNames.Contains(call.Name, StringComparer.Ordinal))) {
            return Prepare(call.Name, call.Arguments);
        }
        var original = FindCompletedContextProposal(saved, Segment, dispatchBatch, call);
        if (original?.Payload.SourcePreparation is not { } source) {
            return Prepare(call.Name, call.Arguments);
        }
        var payload = Prepare(call.Name, call.Arguments, allowDeferredContextTool: true, persistedSource: source);
        if (payload != original.Payload) {
            throw Denied("The replayed context call differs from its exact completed proposal.");
        }
        return payload;
    }

    internal static AgentToolProposalRecord? FindCompletedContextProposal(AgentToolJournalRecord saved,
        AgentToolInvocationSegment segment, AgentToolBatchId? batchId, FunctionCallContent call) {
        AgentToolProposalRecord? proposal;
        if (batchId is { } activeBatch) {
            proposal = saved.Batches.SingleOrDefault(batch => batch.Id == activeBatch)?.Proposals
                .SingleOrDefault(item => item.CallId == call.CallId && item.Payload.ToolName == call.Name);
        } else {
            var previous = saved.Segments.SingleOrDefault(item => item.Id == segment.ContinuesSegmentId);
            var binding = previous?.PendingApprovals.SingleOrDefault(item =>
                item.CallId == call.CallId && item.ToolName == call.Name)?.ToolAdmission;
            proposal = binding is null ? null : AgentToolJournalTransitions.RequireProposal(saved, binding);
        }
        return proposal?.State == AgentToolProposalState.Completed ? proposal : null;
    }

    internal async Task<AgentToolProtocolEnvelope?> ReplayResponseAsync(AgentToolSemanticDigest requestDigest,
        CancellationToken cancellationToken) {
        await CheckRecoveryAsync(cancellationToken);
        var saved = await journal.ReadAsync(lease, cancellationToken);
        if (responseCursor == saved.Batches.Length) {
            if (saved.HasUnresolvedEffects && !CanContinueNativeApprovals(saved)) {
                throw Denied("Drain or reconcile the saved serial batch before requesting another model proposal.");
            }

            return null;
        }

        var batch = saved.Batches[responseCursor++];
        if (batch.RequestDigest != requestDigest) {
            throw Denied("The regenerated provider request differs from the saved SDK protocol; explicit recovery is required.");
        }

        dispatchBatch = batch.Id;
        return batch.Response;
    }

    internal async Task AdmitResponseAsync(AgentToolSemanticDigest requestDigest, AgentToolProtocolEnvelope response,
        IReadOnlyList<AIContent> contents, CancellationToken cancellationToken,
        AgentToolProviderDispatchRecord? providerDispatch = null) {
        var handled = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet(StringComparer.Ordinal);
        var prepared = contents.SelectMany(content => content switch {
            FunctionCallContent { InformationalOnly: false } call when !handled.Contains(call.CallId)
                => new[] { PrepareFunction(call, requiresApproval: false) },
            ToolApprovalRequestContent { ToolCall: FunctionCallContent { InformationalOnly: false } call }
                => new[] { PrepareFunction(call, requiresApproval: true) },
            ToolApprovalRequestContent approval => new[] { MafNativeToolContracts.PrepareApproval(approval, nativeTools) },
            _ => Array.Empty<AgentToolPreparedCall>()
        }).ToArray();
        if (prepared.Any(call => call.ProviderCall is null && call.RequiresApproval)) {
            prepared = prepared.Select(call => call.ProviderCall is null ? call with { RequiresApproval = true } : call).ToArray();
        }
        var saved = providerDispatch is null
            ? await journal.AdmitBatchAsync(lease, requestDigest, response, prepared, cancellationToken)
            : await journal.CompleteProviderDispatchAsync(lease, providerDispatch.Id, requestDigest, response, prepared, cancellationToken);
        var batch = saved.Batches[^1];
        HasActiveProviderDispatch = false;
        dispatchBatch = batch.Id;
        responseCursor = batch.Ordinal + 1;
    }

    internal async Task<AgentToolProviderDispatchRecord?> BeginProviderRequestAsync(AgentToolSemanticDigest requestDigest,
        IReadOnlyList<ChatMessage> input, IReadOnlyList<MafNativeToolContract> nativeContracts,
        CancellationToken cancellationToken) {
        if (nativeContracts.Count == 0) {
            return null;
        }

        var saved = await journal.ReadAsync(lease, cancellationToken);
        var approved = new List<AgentToolApprovalBinding>();
        foreach (var response in input.SelectMany(message => message.Contents).OfType<ToolApprovalResponseContent>()
            .Where(response => response.ToolCall is McpServerToolCallContent)) {
            var matches = saved.Batches.SelectMany(batch => batch.Proposals.Select(proposal => (batch.Id, Proposal: proposal)))
                .Where(item => item.Proposal.ProviderCall is not null && item.Proposal.ApprovalId == response.RequestId).ToArray();
            if (matches.Length != 1) {
                throw Denied("The native approval response does not identify exactly one saved proposal.");
            }

            var (batchId, proposal) = matches[0];
            var prepared = MafNativeToolContracts.PrepareApproval(new(response.RequestId, response.ToolCall), nativeTools);
            if (prepared.Payload != proposal.Payload || response.ToolCall.CallId != proposal.CallId ||
                response.Approved != (proposal.ApprovalStatus == ExecutionApprovalStatus.Approved) ||
                proposal.ApprovalStatus == ExecutionApprovalStatus.Pending) {
                throw Denied("The native approval response differs from its exact durable decision or prepared payload.");
            }

            if (proposal.State == AgentToolProposalState.Prepared && response.Approved) {
                approved.Add(new(batchId, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest));
            }
        }

        var dispatch = await journal.BeginProviderDispatchAsync(lease, Segment.Id, requestDigest,
            MafToolProtocolCodec.Digest(nativeContracts), approved, cancellationToken);
        HasActiveProviderDispatch = true;
        return dispatch;
    }

    private AgentToolPreparedCall PrepareFunction(FunctionCallContent call, bool requiresApproval) {
        var payload = Prepare(call.Name, call.Arguments);
        return new(call.CallId, payload, requiresApproval || tools[call.Name].GetService<ApprovalRequiredAIFunction>() is not null ||
            payload.Effect == AgentToolProposalEffect.SensitiveDisclosure);
    }

    private bool CanContinueNativeApprovals(AgentToolJournalRecord saved) {
        if (saved.HasUnresolvedProviderDispatch) {
            return false;
        }

        var previous = saved.Segments.SingleOrDefault(segment => segment.Id == Segment.ContinuesSegmentId);
        return previous is not null && saved.Batches.All(batch => batch.Proposals.All(proposal =>
            proposal.State is AgentToolProposalState.Completed or AgentToolProposalState.Rejected ||
            proposal.State == AgentToolProposalState.Prepared && proposal.ProviderCall is not null &&
            proposal.ApprovalStatus == ExecutionApprovalStatus.Approved &&
            previous.PendingApprovals.Any(approval => approval.ToolAdmission?.IntentId == proposal.IntentId)));
    }

    private static void RestoreNativeApprovalInputs(AgentToolJournalRecord saved, List<ChatMessage> input) {
        foreach (var message in input) {
            for (var index = 0; index < message.Contents.Count; index++) {
                if (message.Contents[index] is not ToolApprovalResponseContent response || response.ToolCall is not McpServerToolCallContent) {
                    continue;
                }

                var proposal = saved.Batches.SelectMany(batch => batch.Proposals).SingleOrDefault(proposal =>
                    proposal.ProviderCall is not null && proposal.ApprovalId == response.RequestId)
                    ?? throw Denied("The native approval input has no original admitted protocol envelope.");
                message.Contents[index] = MafNativeToolContracts.RestoreApproval(proposal).CreateResponse(response.Approved);
            }
        }
    }

    internal async ValueTask<object?> InvokeAsync(FunctionCallContent call, Func<CancellationToken, ValueTask<object?>> invoke,
        AgentToolInvocationEffectScope effectScope, CancellationToken cancellationToken) {
        var payload = PrepareInvocation(call);
        var batch = dispatchBatch ?? FindContinuationBatch(await journal.ReadAsync(lease, cancellationToken), call, payload);
        var claim = await journal.ClaimInvocationAsync(lease, batch, call.CallId, payload, cancellationToken);
        using var dispatch = claim.Bind();
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            await PauseForPendingNativeApprovalAsync(cancellationToken);
            await using var disclosure = await AuthorizeDisclosureAsync(claim.Proposal, cancellationToken);
            return RestoreResult(claim.Proposal.Result ?? throw Denied("The completed invocation has no saved result."));
        }

        async ValueTask<object?> InvokeAuthorizedAsync() {
            IAsyncDisposable? authorization;
            try {
                authorization = await AuthorizeAsync(payload, cancellationToken);
            } catch (Exception exception) when (claim.Proposal.State == AgentToolProposalState.Prepared &&
                effectScope.CommittedEffect is null &&
                exception is AgentToolAdmissionException or AgentToolPolicyBlockedException or UnauthorizedAccessException) {
                return RecordPolicyDenial("Current execution authority denied this saved proposal.");
            }
            await using var authorizationScope = authorization;
            try {
                return await invoke(cancellationToken);
            } catch (Exception exception) when (effectScope.CommittedEffect is null &&
                MafAgentToolFailureMapper.TryMap(exception, out var failure) &&
                failure.EffectState is AgentToolEffectState.NotCommitted or AgentToolEffectState.None) {
                return failure;
            } catch (AgentToolPolicyBlockedException) when (claim.Proposal.State == AgentToolProposalState.Prepared &&
                effectScope.CommittedEffect is null) {
                return RecordPolicyDenial("Current execution policy denied this saved proposal.");
            }
        }

        try {
            var result = await InvokeAuthorizedAsync();
            if (claim.Proposal.State != AgentToolProposalState.Prepared &&
                (effectScope.PreDispatchFailure is not null || result is AgentToolFailureResult {
                    Succeeded: false, EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted
                })) {
                throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                    "A current denial cannot resolve the earlier dispatched effect. The original outcome evidence requires owner reconciliation.");
            }

            var checkpoint = CaptureResult(result, effectScope.PreDispatchFailure);
            var effect = effectScope.CommittedEffect is not null ? AgentToolEffectState.Committed :
                MafRuntimeToolInvocationResultClassifier.Assess(call.Name,
                    toolPolicies.Classify(call.Name), result, effectScope.PreDispatchFailure).EffectState;
            var requiresReconciliation = result is IAgentToolOwnerObservationEvidence { RequiresOwnerReconciliation: true };
            await journal.CompleteInvocationAsync(claim, checkpoint, effect, cancellationToken, requiresReconciliation,
                effectScope.DisclosureEvidence);
            if (!requiresReconciliation) {
                completedInCurrentInvocation.Add(claim.Proposal.IntentId);
            }
            await PauseForPendingNativeApprovalAsync(cancellationToken);
            return RestoreResult(checkpoint);
        } catch {
            try {
                await journal.PreserveUncertainInvocationAsync(claim, CancellationToken.None);
            } catch {
                // A failed checkpoint cannot replace the original invocation exception or authorize a retry.
            }

            throw;
        }
    }

    private static AgentToolFailureResult RecordPolicyDenial(string message) {
        var denied = new AgentToolFailureResult(false, "ToolPolicyDenied", message, false) {
            EffectState = AgentToolEffectState.NotCommitted
        };
        AgentToolInvocationEffectScope.RecordPreDispatchFailure(new(denied.ErrorCode, denied.Message));
        return denied;
    }

    internal async Task<IReadOnlyList<PendingToolApprovalRecord>> SaveApprovalsAsync(string serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> pending, CancellationToken cancellationToken) {
        var checkpoint = MafToolProtocolCodec.Encode(serializedSessionStateJson);
        return await journal.SaveApprovalCheckpointAsync(lease, Segment.Id, checkpoint, serializedSessionStateJson,
            pending, cancellationToken);
    }

    private async Task PauseForPendingNativeApprovalAsync(CancellationToken cancellationToken) {
        var saved = await journal.ReadAsync(lease, cancellationToken);
        var proposals = saved.Batches.Skip(Segment.FirstBatchOrdinal).SelectMany(batch => batch.Proposals).ToArray();
        if (!proposals.Any(proposal => proposal.ProviderCall is not null && proposal.ApprovalStatus == ExecutionApprovalStatus.Pending) ||
            proposals.Any(proposal => proposal.ProviderCall is null && proposal.State is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected))) {
            return;
        }

        var invocation = FunctionInvokingChatClient.CurrentContext
            ?? throw Denied("The SDK invocation has no control context to publish its pending native approval safely.");
        invocation.Terminate = true;
    }

    private async Task CheckRecoveryAsync(CancellationToken cancellationToken) {
        var saved = await journal.ReadAsync(lease, cancellationToken);
        recoveryState = saved;
        if (saved.HasUnresolvedProviderDispatch) {
            throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                "A provider request may already have executed a hosted tool. Its missing response requires reconciliation before any provider replay.");
        }

        foreach (var proposal in saved.Batches.SelectMany(batch => batch.Proposals)) {
            if (proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired &&
                proposal.Payload.Recovery == AgentToolProposalRecovery.ReconcileBeforeRetry) {
                throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                    "A previously dispatched non-idempotent tool is uncertain. Reconcile it before provider replay or any effect.");
            }

            if (proposal.ProviderCall is not null) {
                var prepared = MafNativeToolContracts.PrepareApproval(MafNativeToolContracts.RestoreApproval(proposal), nativeTools);
                if (prepared.Payload != proposal.Payload || prepared.ProviderCall?.ContractDigest != proposal.ProviderCall.ContractDigest) {
                    throw Denied("The current native contract differs from the admitted provider proposal.");
                }

                continue;
            }

            using var arguments = JsonDocument.Parse(proposal.Payload.ArgumentsJson);
            var values = arguments.RootElement.Deserialize<Dictionary<string, object?>>(MafToolProtocolCodec.SerializationOptions);
            if (Prepare(proposal.Payload.ToolName, values, allowDeferredContextTool: true,
                    persistedSource: proposal.Payload.SourcePreparation) != proposal.Payload) {
                throw Denied("The installed provider preparation policy no longer matches the admitted payload.");
            }
            if (proposal.State != AgentToolProposalState.Completed && proposal.Payload.SourcePreparation is { } source &&
                    !(metadata.TryGetValue(proposal.Payload.ToolName, out var descriptor) && descriptor.PrepareAdmission is not null)) {
                var preparation = contextToolRegistrations.SingleOrDefault(registration =>
                    registration.ToolNames.Contains(proposal.Payload.ToolName, StringComparer.Ordinal))?.SourcePreparation
                    ?? throw MafContextToolSourceContract.MissingSource();
                await preparation.ValidateAsync(source, cancellationToken);
            }

            await using var authorization = proposal.State == AgentToolProposalState.Completed
                ? await AuthorizeDisclosureAsync(proposal, cancellationToken, allowFreshResult: true)
                : await AuthorizeAsync(proposal.Payload, cancellationToken);
        }
    }

    private ValueTask<IAsyncDisposable?> AuthorizeDisclosureAsync(AgentToolProposalRecord proposal,
        CancellationToken cancellationToken, bool allowFreshResult = false) {
        if (allowFreshResult && completedInCurrentInvocation.Contains(proposal.IntentId)) {
            return ValueTask.FromResult<IAsyncDisposable?>(null);
        }
        var authorize = metadata.TryGetValue(proposal.Payload.ToolName, out var descriptor)
            ? descriptor.AuthorizeResultDisclosureAsync : null;
        authorize ??= contextToolRegistrations.SingleOrDefault(registration =>
            registration.ToolNames.Contains(proposal.Payload.ToolName, StringComparer.Ordinal))?.AuthorizeResultDisclosureAsync;
        if (authorize is not null) {
            var result = MafToolProtocolCodec.Decode<ResultCheckpoint>(proposal.Result
                ?? throw Denied("The completed invocation has no saved result."));
            return authorize(new(proposal.IntentId, proposal.Payload, proposal.EffectState, result.Value,
                proposal.DisclosureEvidence), cancellationToken);
        }

        if (completedInCurrentInvocation.Contains(proposal.IntentId)) {
            return ValueTask.FromResult<IAsyncDisposable?>(null);
        }

        throw new AgentToolAdmissionException("tool-admission.disclosure-authorization-unavailable",
            "This saved tool result has no current owner disclosure check. Explicit recovery is required; the completed effect will not be repeated.");
    }

    private ValueTask<IAsyncDisposable?> AuthorizeAsync(AgentToolPreparedPayload payload, CancellationToken cancellationToken)
        => metadata.TryGetValue(payload.ToolName, out var descriptor) && descriptor.AuthorizeAdmissionAsync is { } authorize
            ? AcquireAsync(authorize, payload, cancellationToken)
            : ValueTask.FromResult<IAsyncDisposable?>(null);

    private static async ValueTask<IAsyncDisposable?> AcquireAsync(
        Func<AgentToolPreparedPayload, CancellationToken, ValueTask<IAsyncDisposable>> authorize,
        AgentToolPreparedPayload payload, CancellationToken cancellationToken)
        => await authorize(payload, cancellationToken);

    private AgentToolBatchId FindContinuationBatch(AgentToolJournalRecord saved, FunctionCallContent call,
        AgentToolPreparedPayload payload) {
        var previous = saved.Segments.SingleOrDefault(segment => segment.Id == Segment.ContinuesSegmentId)
            ?? throw Denied("The tool call has no saved SDK continuation segment.");
        var approval = previous.PendingApprovals.SingleOrDefault(item => item.CallId == call.CallId && item.ToolName == call.Name);
        var binding = approval?.ToolAdmission ?? throw Denied("The SDK approval call does not identify one saved proposal.");
        if (AgentToolJournalTransitions.RequireProposal(saved, binding).Payload != payload) {
            throw Denied("The SDK approval call differs from its approved semantic payload.");
        }

        return binding.BatchId;
    }

    private static AgentToolProtocolEnvelope CaptureResult(object? value, AgentToolPreDispatchFailure? failure = null) {
        if (failure is not null) {
            if (value is AgentToolFailureResult hostFailure) {
                RequireHostFailure(hostFailure, failure);
            } else if (value is not string) {
                throw Denied("A pre-dispatch denial requires its original text or typed host failure.");
            }
        }
        var kind = value switch {
            string when failure is not null => ResultKind.PreDispatchDeniedText,
            AgentToolFailureResult when failure is not null => ResultKind.PreDispatchDeniedJson,
            AgentToolFailureResult => ResultKind.TypedFailureJson,
            null => ResultKind.Null,
            string => ResultKind.Text,
            AIContent => ResultKind.Content,
            IEnumerable<AIContent> => ResultKind.Contents,
            _ => ResultKind.Json
        };
        var element = kind switch {
            ResultKind.Content => JsonSerializer.SerializeToElement((AIContent)value!, MafToolProtocolCodec.SerializationOptions),
            ResultKind.Contents => JsonSerializer.SerializeToElement(((IEnumerable<AIContent>)value!).ToArray(), MafToolProtocolCodec.SerializationOptions),
            _ => JsonSerializer.SerializeToElement(value, MafToolProtocolCodec.SerializationOptions)
        };
        return MafToolProtocolCodec.Encode(new ResultCheckpoint(kind, element) { PreDispatchFailure = failure });
    }

    private static object? RestoreResult(AgentToolProtocolEnvelope saved) {
        var result = MafToolProtocolCodec.Decode<ResultCheckpoint>(saved);
        if ((result.Kind is ResultKind.PreDispatchDeniedText or ResultKind.PreDispatchDeniedJson) != (result.PreDispatchFailure is not null)) {
            throw Denied("The saved pre-dispatch denial has incompatible outcome evidence.");
        }
        if (result.PreDispatchFailure is { } failure) {
            if (result.Kind == ResultKind.PreDispatchDeniedJson) {
                var hostFailure = result.Value.Deserialize<AgentToolFailureResult>(MafToolProtocolCodec.SerializationOptions)
                    ?? throw Denied("The saved pre-dispatch host failure is empty.");
                RequireHostFailure(hostFailure, failure);
            }
            AgentToolInvocationEffectScope.RecordPreDispatchFailure(failure);
        }
        return result.Kind switch {
            ResultKind.Null => null,
            ResultKind.Text or ResultKind.PreDispatchDeniedText => result.Value.GetString(),
            ResultKind.Content => result.Value.Deserialize<AIContent>(MafToolProtocolCodec.SerializationOptions),
            ResultKind.Contents => result.Value.Deserialize<AIContent[]>(MafToolProtocolCodec.SerializationOptions),
            ResultKind.Json or ResultKind.PreDispatchDeniedJson => result.Value,
            ResultKind.TypedFailureJson => result.Value.Deserialize<AgentToolFailureResult>(MafToolProtocolCodec.SerializationOptions)
                ?? throw Denied("The saved typed tool failure is empty."),
            _ => throw Denied("The saved tool result shape is unsupported.")
        };
    }

    private static void RequireHostFailure(AgentToolFailureResult hostFailure, AgentToolPreDispatchFailure captured) {
        if (hostFailure.Succeeded || hostFailure.EffectState != AgentToolEffectState.NotCommitted ||
                hostFailure.ErrorCode != captured.FailureCode || hostFailure.Message != captured.SafeMessage ||
                hostFailure.CanRetryWithCorrectedInput != captured.CanRetryWithCorrectedInput) {
            throw Denied("The saved pre-dispatch host failure differs from its trusted outcome evidence.");
        }
    }

    private static AgentToolAdmissionException Denied(string message) => new("tool-admission.runtime-denied", message);
    private enum ResultKind { Null, Text, Content, Contents, Json, PreDispatchDeniedText, PreDispatchDeniedJson, TypedFailureJson }
    private sealed record ResultCheckpoint(ResultKind Kind, JsonElement Value) {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AgentToolPreDispatchFailure? PreDispatchFailure { get; init; }
    }
    private sealed record RestartCheckpoint(string SerializedSessionStateJson, List<ChatMessage> Input,
        bool IsApprovalContinuation, PendingToolApprovalRecord[] PendingApprovals);
    private sealed class RestoreScope(MafToolRunContext? previous) : IDisposable {
        public void Dispose() => Ambient.Value = previous;
    }
}
