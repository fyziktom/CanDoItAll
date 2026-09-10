using CanDoItAll.AgentFramework.Runtime.Abstractions;
using System.Text.Json;
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
    private readonly IReadOnlyDictionary<string, AITool> tools;
    private int responseCursor;
    private AgentToolBatchId? dispatchBatch;

    private MafToolRunContext(AgentToolAdmissionJournal journal, AgentToolRunLease lease,
        AgentToolInvocationSegment segment, RuntimeCapabilityState? capabilities) {
        this.journal = journal;
        toolPolicies = capabilities?.ToolPolicies ?? AgentToolPolicyCatalog.BuiltIn;
        this.lease = lease;
        Segment = segment;
        responseCursor = segment.FirstBatchOrdinal;
        metadata = (capabilities?.RuntimeToolMetadata ?? []).ToDictionary(item => item.ToolName, StringComparer.Ordinal);
        tools = (capabilities?.Tools ?? []).ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    internal static MafToolRunContext? Current => Ambient.Value;
    internal AgentToolInvocationSegment Segment { get; }

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
                provider, model, options, chat.Compatibility?.PendingApprovals ?? [], progress, cancellationToken)
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

    internal AgentToolPreparedPayload Prepare(string name, IDictionary<string, object?>? arguments) {
        if (!tools.ContainsKey(name)) {
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
        return new(name, 1, MafToolProtocolCodec.Digest(new { Name = name, Arguments = element }), json, effect,
            effect == AgentToolProposalEffect.Read ? AgentToolProposalRecovery.RevalidateAndRead : AgentToolProposalRecovery.ReconcileBeforeRetry);
    }

    internal async Task<AgentToolProtocolEnvelope?> ReplayResponseAsync(AgentToolSemanticDigest requestDigest,
        CancellationToken cancellationToken) {
        await CheckRecoveryAsync(cancellationToken);
        var saved = await journal.ReadAsync(lease, cancellationToken);
        if (responseCursor == saved.Batches.Length) {
            if (saved.HasUnresolvedEffects) {
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
        IReadOnlyList<FunctionCallContent> calls, CancellationToken cancellationToken) {
        var prepared = calls.Select(call => {
            var payload = Prepare(call.Name, call.Arguments);
            var requiresApproval = tools[call.Name] is ApprovalRequiredAIFunction ||
                payload.Effect == AgentToolProposalEffect.SensitiveDisclosure;
            return new AgentToolPreparedCall(call.CallId, payload, requiresApproval);
        }).ToArray();
        var saved = await journal.AdmitBatchAsync(lease, requestDigest, response, prepared, cancellationToken);
        var batch = saved.Batches[^1];
        dispatchBatch = batch.Id;
        responseCursor = batch.Ordinal + 1;
    }

    internal async ValueTask<object?> InvokeAsync(FunctionCallContent call, Func<CancellationToken, ValueTask<object?>> invoke,
        AgentToolInvocationEffectScope effectScope, CancellationToken cancellationToken) {
        var payload = Prepare(call.Name, call.Arguments);
        var batch = dispatchBatch ?? FindContinuationBatch(await journal.ReadAsync(lease, cancellationToken), call, payload);
        var claim = await journal.ClaimInvocationAsync(lease, batch, call.CallId, payload, cancellationToken);
        using var dispatch = claim.Bind();
        await using var authorization = await AuthorizeAsync(payload, cancellationToken);
        if (claim.Proposal.State == AgentToolProposalState.Completed) {
            return RestoreResult(claim.Proposal.Result ?? throw Denied("The completed invocation has no saved result."));
        }

        try {
            object? result;
            try {
                result = await invoke(cancellationToken);
            } catch (Exception exception) when (effectScope.CommittedEffect is null &&
                MafAgentToolFailureMapper.TryMap(exception, out var failure) &&
                failure.EffectState is AgentToolEffectState.NotCommitted or AgentToolEffectState.None) {
                result = failure;
            } catch (AgentToolPolicyBlockedException) {
                var denied = new AgentToolFailureResult(false, "ToolPolicyDenied", "Current execution policy denied this saved proposal.", false) {
                    EffectState = AgentToolEffectState.NotCommitted
                };
                await journal.CompleteInvocationAsync(claim, CaptureResult(denied), AgentToolEffectState.NotCommitted, cancellationToken);
                throw;
            }

            var checkpoint = CaptureResult(result);
            var effect = effectScope.CommittedEffect is not null ? AgentToolEffectState.Committed :
                MafRuntimeToolInvocationResultClassifier.Assess(call.Name,
                    toolPolicies.Classify(call.Name), result).EffectState;
            await journal.CompleteInvocationAsync(claim, checkpoint, effect, cancellationToken);
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

    internal async Task<IReadOnlyList<PendingToolApprovalRecord>> SaveApprovalsAsync(string serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> pending, CancellationToken cancellationToken) {
        var checkpoint = MafToolProtocolCodec.Encode(serializedSessionStateJson);
        return await journal.SaveApprovalCheckpointAsync(lease, Segment.Id, checkpoint, serializedSessionStateJson,
            pending, cancellationToken);
    }

    private async Task CheckRecoveryAsync(CancellationToken cancellationToken) {
        var saved = await journal.ReadAsync(lease, cancellationToken);
        foreach (var proposal in saved.Batches.SelectMany(batch => batch.Proposals)) {
            if (proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired &&
                proposal.Payload.Recovery == AgentToolProposalRecovery.ReconcileBeforeRetry) {
                throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                    "A previously dispatched non-idempotent tool is uncertain. Reconcile it before provider replay or any effect.");
            }

            using var arguments = JsonDocument.Parse(proposal.Payload.ArgumentsJson);
            var values = arguments.RootElement.Deserialize<Dictionary<string, object?>>(MafToolProtocolCodec.SerializationOptions);
            if (Prepare(proposal.Payload.ToolName, values) != proposal.Payload) {
                throw Denied("The installed provider preparation policy no longer matches the admitted payload.");
            }

            await using var authorization = await AuthorizeAsync(proposal.Payload, cancellationToken);
        }
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

    private static AgentToolProtocolEnvelope CaptureResult(object? value) {
        var kind = value switch {
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
        return MafToolProtocolCodec.Encode(new ResultCheckpoint(kind, element));
    }

    private static object? RestoreResult(AgentToolProtocolEnvelope saved) {
        var result = MafToolProtocolCodec.Decode<ResultCheckpoint>(saved);
        return result.Kind switch {
            ResultKind.Null => null,
            ResultKind.Text => result.Value.GetString(),
            ResultKind.Content => result.Value.Deserialize<AIContent>(MafToolProtocolCodec.SerializationOptions),
            ResultKind.Contents => result.Value.Deserialize<AIContent[]>(MafToolProtocolCodec.SerializationOptions),
            ResultKind.Json => result.Value,
            _ => throw Denied("The saved tool result shape is unsupported.")
        };
    }

    private static AgentToolAdmissionException Denied(string message) => new("tool-admission.runtime-denied", message);
    private enum ResultKind { Null, Text, Content, Contents, Json }
    private sealed record ResultCheckpoint(ResultKind Kind, JsonElement Value);
    private sealed record RestartCheckpoint(string SerializedSessionStateJson, List<ChatMessage> Input,
        bool IsApprovalContinuation, PendingToolApprovalRecord[] PendingApprovals);
    private sealed class RestoreScope(MafToolRunContext? previous) : IDisposable {
        public void Dispose() => Ambient.Value = previous;
    }
}
