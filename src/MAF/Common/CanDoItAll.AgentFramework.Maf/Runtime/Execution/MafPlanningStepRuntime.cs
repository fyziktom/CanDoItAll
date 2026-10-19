using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafPlanningStepRuntime(IMafProviderAgentFactory providers, ILogger<MafPlanningStepRuntime> logger) : IAgentPlanningStepRuntime {
    private const string UsagePhase = "planning-step";

    public AgentToolSemanticDigest CaptureRuntimeContract(AgentDefinition agent, ProviderProfile provider, IAgentPlanningParticipant participant)
        => MafPlanningRuntimeContract.Capture(agent, provider, participant);

    public async Task<AgentPlanningStepResult> RunAsync(AgentPlanningStepRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        MafPlanningRuntimeContract.Require(request);
        var original = await request.DecisionOwner.ObserveStepAsync(request.Work, cancellationToken);
        var scope = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(original.Run.MetadataJson)?.WorkspaceScope;
        var history = AgentHistoryInvocation.Create(original.Run, scope);
        AgentPlanningStepResult? result = null;
        var response = await HistoryAgentRuntime.ObserveAsync(history, async () => {
            result = await RunCoreAsync(request, original, history, cancellationToken);
            return result.RuntimeResponse;
        });
        return result! with { RuntimeResponse = response };
    }

    private async Task<AgentPlanningStepResult> RunCoreAsync(AgentPlanningStepRequest request, AgentPlanningStepObservation original,
        HistoryInvocationContext history, CancellationToken cancellationToken) {
        var run = original.Run;
        var decision = run.ToolAdmission!.PlanningDecision!;
        var model = MafModelParametersBuilder.ResolveRuntimeModel(request.Agent, request.Provider);
        if (run.AgentId != request.Agent.Id || run.ProviderProfileId != request.Provider.Id || run.Model != model ||
            request.Participant.Owner != decision.Owner) {
            throw MafPlanningStepContext.Failure("The selected Agent, provider, model or module does not match the original decision.");
        }
        var prompt = request.Participant.Prompt;
        var options = MafPlanningRuntimeContract.Options(request.Agent, request.Provider, prompt);
        options = ProviderHistoryChatContext.WithContext(options, history);
        var input = CreateInput(original, prompt);
        var agent = providers.CreateFrameworkAgent(request.Provider, model, new ChatClientAgentOptions {
            Name = request.Agent.Name, ChatOptions = options, UseProvidedChatClientAsIs = true,
            AllowConcurrentInvocation = false, ThrowOnChatHistoryProviderConflict = true
        }, frameworkManagedHistory: true, allowBackgroundResponses: false);
        Exception? originalFailure = null;
        try {
            if (agent.GetService<MafToolAdmissionChatClient>() is null) {
                throw MafPlanningStepContext.Failure("The configured provider is missing the required planning admission boundary.");
            }
            var runtimeSession = await agent.CreateSessionAsync(cancellationToken);
            var requestDigest = MafToolAdmissionChatClient.RequestDigest(input, options, []);
            var contract = request.ExpectedRuntimeContract;
            var context = new MafPlanningStepContext(request, original, requestDigest, contract, logger);
            using var bound = context.Bind();
            var response = await agent.RunAsync(input, runtimeSession, cancellationToken: cancellationToken);
            var admitted = context.Admission ?? throw MafPlanningStepContext.Failure("The configured provider did not pass through planning admission.");
            var usage = MafRuntimeResponseAssembler.CreateProviderUsageObservation(request.Provider, model, runtimeSession,
                request.Work.StepId.ToString("N"), response, UsagePhase, "Original planning input and response recorded.");
            var result = MafRuntimeResponseMapper.CreateTerminalRuntimeResponse(response.Text, response, response,
                request.Work.StepId.ToString("N"), null, [], [], [], [usage]);
            return new(admitted, result, context.OutputRefusal);
        } catch (Exception failure) {
            originalFailure = failure;
            throw;
        } finally {
            try {
                if (agent is IAsyncDisposable asyncDisposable) {
                    await asyncDisposable.DisposeAsync();
                } else if (agent is IDisposable disposable) {
                    disposable.Dispose();
                } else {
                    agent.GetService<IChatClient>()?.Dispose();
                }
            } catch (Exception cleanupFailure) when (originalFailure is not null) {
                throw new AggregateException("Planning failed and its provider lifetime also failed to close. The original journal is retained.",
                    originalFailure, cleanupFailure);
            }
        }
    }

    private static ChatMessage[] CreateInput(AgentPlanningStepObservation original, AgentPlanningPromptContract prompt) {
        var decision = original.Run.ToolAdmission!.PlanningDecision!;
        var content = JsonSerializer.Serialize(new {
            decision.DecisionId, decision.CurrentGeneration, StepId = decision.Current.Steps[^1].Id,
            OriginalRequest = original.Run.ToolAdmission.OriginalInput!.Content,
            OriginalTarget = JsonSerializer.Deserialize<JsonElement>(decision.OriginalTarget.PayloadJson),
            MaterialBasis = JsonSerializer.Deserialize<JsonElement>(original.Basis.Value.PayloadJson)
        });
        if (Encoding.UTF8.GetByteCount(content) + Encoding.UTF8.GetByteCount(prompt.Instructions) > AgentPlanningProviderBounds.MaximumInputUtf8Bytes) {
            throw MafPlanningStepContext.Failure("The complete original planning input exceeds its provider-input bound; no partial basis is sent.");
        }
        return [new(ChatRole.System, prompt.Instructions) { MessageId = Guid.NewGuid().ToString("N") },
            new(ChatRole.User, content) { MessageId = Guid.NewGuid().ToString("N") }];
    }
}
