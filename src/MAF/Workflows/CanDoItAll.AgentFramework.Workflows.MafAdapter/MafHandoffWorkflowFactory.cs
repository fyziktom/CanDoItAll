using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafHandoffWorkflowBuildResult(
    AIAgent Agent,
    Guid EntryAgentId,
    IReadOnlyList<AgentHandoffRouteSettings> Routes,
    Func<AgentResponseUpdate, bool> IsTerminalResponseUpdate);

internal static class MafHandoffWorkflowFactory
{
    public static MafHandoffWorkflowBuildResult Build(
        AgentHandoffSettings settings,
        IReadOnlyDictionary<Guid, AIAgent> agents,
        Guid entryAgentId,
        string correlationId,
        IWorkflowExecutionEnvironment? executionEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(agents);

        var validation = AgentHandoffMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent handoff configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var normalized = AgentHandoffMetadata.Normalize(settings);
        if (!agents.TryGetValue(entryAgentId, out var entryAgent))
        {
            throw new InvalidOperationException($"Handoff entry agent '{entryAgentId:D}' was not included in the runtime participants.");
        }

        foreach (var route in normalized.Routes.Where(route => route.Enabled))
        {
            if (!agents.ContainsKey(route.SourceAgentId))
            {
                throw new InvalidOperationException($"Handoff route source agent '{route.SourceAgentId:D}' was not included in the runtime participants.");
            }

            if (!agents.ContainsKey(route.TargetAgentId))
            {
                throw new InvalidOperationException($"Handoff route target agent '{route.TargetAgentId:D}' was not included in the runtime participants.");
            }
        }

#pragma warning disable MAAIW001
        var builder = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(entryAgent)
            .EmitAgentResponseUpdateEvents(normalized.EmitAgentResponseUpdateEvents)
            .WithToolCallFilteringBehavior(HandoffToolCallFilteringBehavior.HandoffOnly);

        if (normalized.EmitAgentResponseEvents)
        {
            builder.EmitAgentResponseEvents();
        }

        if (normalized.ReturnToPrevious)
        {
            builder.EnableReturnToPrevious();
        }

        if (!string.IsNullOrWhiteSpace(normalized.HandoffInstructions))
        {
            builder.WithHandoffInstructions(normalized.HandoffInstructions);
        }

        foreach (var route in normalized.Routes.Where(route => route.Enabled))
        {
            builder.WithHandoff(
                agents[route.SourceAgentId],
                agents[route.TargetAgentId],
                string.IsNullOrWhiteSpace(route.Reason) ? null : route.Reason);
        }

        var workflow = builder.Build();
        var workflowAgent = workflow.AsAIAgent(
            id: $"handoff-{entryAgentId:D}",
            name: entryAgent.Name is null ? "Handoff workflow" : $"{entryAgent.Name} handoff workflow",
            description: "Microsoft Agent Framework handoff workflow for configured local agents.",
            executionEnvironment: executionEnvironment,
            includeExceptionDetails: false,
            includeWorkflowOutputsInResponse: true);
#pragma warning restore MAAIW001

        return new MafHandoffWorkflowBuildResult(
            new HandoffDepthGuardAgent(
                workflowAgent,
                normalized.MaxHandoffDepth,
                string.IsNullOrWhiteSpace(correlationId) ? entryAgentId.ToString("D") : correlationId),
            entryAgentId,
            normalized.Routes,
            HandoffResponseProjection.IsTerminalResponseUpdate);
    }
}

internal static class HandoffResponseProjection
{
    public static bool IsTerminalResponseUpdate(AgentResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        return update.Role == ChatRole.Assistant &&
               update.Contents.Count > 0 &&
               update.RawRepresentation is WorkflowOutputEvent output &&
               output.GetType() == typeof(WorkflowOutputEvent) &&
               output.Data is IReadOnlyList<ChatMessage> &&
               !output.IsIntermediate();
    }

}

internal sealed class HandoffDepthGuardAgent(
    AIAgent innerAgent,
    int maxHandoffDepth,
    string correlationId) : DelegatingAIAgent(innerAgent)
{
    private const string HandoffToolNamePrefix = "handoff_to_";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await InnerAgent.RunAsync(
            messages,
            session,
            options,
            cancellationToken);
        var guard = new HandoffDepthGuard(maxHandoffDepth, correlationId);
        foreach (var message in response.Messages)
        {
            guard.Observe(new AgentResponseUpdate(message.Role, message.Contents));
        }

        return response;
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var guard = new HandoffDepthGuard(maxHandoffDepth, correlationId);
        await foreach (var update in InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
        {
            guard.Observe(update);
            yield return update;
        }
    }

    private sealed class HandoffDepthGuard(int maxDepth, string correlationId)
    {
        private readonly HashSet<string> observedHandoffCallIds = new(StringComparer.OrdinalIgnoreCase);
        private int handoffCount;

        public void Observe(AgentResponseUpdate update)
        {
            foreach (var functionCall in update.Contents.OfType<FunctionCallContent>())
            {
                if (string.IsNullOrWhiteSpace(functionCall.Name) ||
                    !functionCall.Name.StartsWith(HandoffToolNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var callId = RequireStableCallId(functionCall);
                if (!observedHandoffCallIds.Add(callId))
                {
                    continue;
                }

                handoffCount++;
                if (handoffCount > maxDepth)
                {
                    throw new InvalidOperationException(
                        $"Handoff workflow exceeded maxHandoffDepth {maxDepth}. CorrelationId={correlationId}.");
                }
            }
        }

        private static string RequireStableCallId(FunctionCallContent functionCall)
        {
            if (string.IsNullOrWhiteSpace(functionCall.CallId))
            {
                throw new InvalidOperationException(
                    $"Cannot enforce handoff depth for tool '{functionCall.Name}' without a stable call identifier.");
            }

            return functionCall.CallId;
        }
    }
}
