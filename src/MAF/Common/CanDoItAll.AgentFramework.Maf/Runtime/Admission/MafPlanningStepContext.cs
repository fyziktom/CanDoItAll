using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafPlanningStepContext(AgentPlanningStepRequest request, AgentPlanningStepObservation original,
    AgentToolSemanticDigest expectedRequest, AgentToolSemanticDigest contract, ILogger logger) {
    private static readonly AsyncLocal<MafPlanningStepContext?> current = new();
    private static readonly TimeSpan OutcomePersistenceTimeout = TimeSpan.FromSeconds(20);
    private int entered;

    internal static MafPlanningStepContext? Current => current.Value;
    internal AgentPlanningResponseAdmission? Admission { get; private set; }
    internal AgentPlanningOutputRefusal? OutputRefusal { get; private set; }

    internal IDisposable Bind() {
        if (current.Value is not null || MafToolRunContext.Current is not null) {
            throw Failure("Planning cannot reuse an ordinary tool invocation or another live planning context.");
        }
        current.Value = this;
        return new Binding(this);
    }

    internal async Task<ChatResponse> InvokeAsync(ChatMessage[] input, ChatOptions? options,
        Func<CancellationToken, Task<ChatResponse>> invoke, CancellationToken cancellationToken) {
        MafPlanningRuntimeContract.Require(request);
        if (!ReferenceEquals(Current, this) || MafToolRunContext.Current is not null ||
            options?.Tools is { Count: > 0 } || !string.IsNullOrEmpty(options?.ConversationId) ||
            MafToolAdmissionChatClient.RequestDigest(input, options, []) != expectedRequest ||
            Interlocked.Exchange(ref entered, 1) != 0) {
            throw Failure("The final planning input changed, exposed tools/history, or repeated an entered step.");
        }
        var decision = original.Run.ToolAdmission!.PlanningDecision!;
        await request.Participant.RequireCurrentReadAsync(decision, cancellationToken);
        var dispatch = await request.DecisionOwner.EnterProviderAsync(request.Work, expectedRequest, contract, cancellationToken);
        var enteredStep = await request.DecisionOwner.ObserveStepAsync(request.Work, cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(enteredStep.RemainingWork);
        var response = await invoke(deadline.Token);
        var encoded = MafToolProtocolCodec.Encode(response);
        if (Encoding.UTF8.GetByteCount(encoded.PayloadJson) > AgentPlanningProviderBounds.MaximumResponseUtf8Bytes) {
            throw Failure("The provider response exceeds the original reserved bound. Inspect the saved dispatch before any new work.");
        }
        var prepared = Prepare(response, decision);
        var publication = prepared as AgentPlanningResponsePreparation.Publication;
        OutputRefusal = (prepared as AgentPlanningResponsePreparation.Refused)?.Reason;
        using var completion = new CancellationTokenSource(OutcomePersistenceTimeout);
        Admission = await request.DecisionOwner.AdmitResponseAsync(request.Work, dispatch.Id, dispatch.RequestDigest,
            encoded, publication?.Call, completion.Token, OutputRefusal);
        return response;
    }

    private AgentPlanningResponsePreparation Prepare(ChatResponse response, AgentPlanningDecisionRecord decision) {
        if (response.Messages.SelectMany(message => message.Contents).Any(content => content is not (TextContent or TextReasoningContent or UsageContent))) {
            return new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.UnsupportedContent);
        }
        if (Encoding.UTF8.GetByteCount(response.Text) > AgentPlanningProviderBounds.MaximumTextUtf8Bytes) {
            return new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.LimitExceeded);
        }
        try {
            return request.Participant.PreparePublication(decision, original.Basis, response.Text) switch {
                AgentPlanningResponsePreparation.Publication publication when AgentPlanningProviderBounds.SupportsPublication(publication.Call) => publication,
                AgentPlanningResponsePreparation.Publication => new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.LimitExceeded),
                AgentPlanningResponsePreparation.Continue continuation => continuation,
                AgentPlanningResponsePreparation.Refused refused when Enum.IsDefined(refused.Reason) => refused,
                _ => new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.PreparationFailed)
            };
        } catch (Exception failure) {
            logger.LogError("Planning response preparation failed for decision {DecisionId}, generation {Generation}, step {StepId}: {FailureType}.",
                request.Work.DecisionId, request.Work.Generation, request.Work.StepId, failure.GetType().FullName);
            return new AgentPlanningResponsePreparation.Refused(AgentPlanningOutputRefusal.PreparationFailed);
        }
    }

    internal static AgentToolAdmissionException Failure(string message) => new("planning.provider-boundary", message);

    private sealed class Binding(MafPlanningStepContext owner) : IDisposable {
        public void Dispose() {
            if (ReferenceEquals(current.Value, owner)) {
                current.Value = null;
            }
        }
    }
}
