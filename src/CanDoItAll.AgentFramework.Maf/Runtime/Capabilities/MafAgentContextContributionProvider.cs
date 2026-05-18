using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafAgentContextContributionProvider(
    IAgentContextContributor contributor,
    AgentDefinition agent,
    ProviderProfile provider,
    AgentContextContributionPolicy policy,
    IAgentContextContributionTraceSink? traceSink = null) : MessageAIContextProvider
{
    public AgentContextContributorId ContributorId => contributor.Descriptor.Id;

    internal async ValueTask<IReadOnlyList<ChatMessage>> ContributeAsync(
        IReadOnlyList<ChatMessage> requestMessages,
        CancellationToken cancellationToken = default)
    {
        var request = new AgentContextContributionRequest(
            agent,
            provider,
            requestMessages.Select(MapRequestMessage).ToList(),
            policy);

        AgentContextContributionResult result;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            result = await contributor.ContributeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            traceSink?.Record(new AgentContextContributionTrace(
                ContributorId,
                AgentContextContributionStatus.Failed,
                GeneratedMessageCount: 0,
                new Dictionary<string, string>(StringComparer.Ordinal),
                WorkflowExecutorRedaction.RedactText(exception.Message),
                stopwatch.Elapsed));
            throw new AgentContextContributionException(
                ContributorId,
                $"Agent context contributor '{ContributorId}' failed while building MAF context.",
                exception);
        }
        stopwatch.Stop();
        traceSink?.Record(new AgentContextContributionTrace(
            ContributorId,
            result.Status,
            result.Messages.Count,
            result.TraceMetadata,
            result.FailureMessage,
            stopwatch.Elapsed));

        return result.Status switch
        {
            AgentContextContributionStatus.Provided => result.Messages.Select(MapChatMessage).ToList(),
            AgentContextContributionStatus.Skipped => [],
            AgentContextContributionStatus.Failed => throw new AgentContextContributionException(
                ContributorId,
                $"Agent context contributor '{ContributorId}' reported failure: {result.FailureMessage}"),
            _ => throw new AgentContextContributionException(
                ContributorId,
                $"Agent context contributor '{ContributorId}' returned unsupported status '{result.Status}'.")
        };
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
        => await ContributeAsync(context.RequestMessages.ToList(), cancellationToken);

    private static AgentContextRequestMessage MapRequestMessage(ChatMessage message)
        => new(MapRole(message.Role), message.Text ?? string.Empty);

    private static ChatMessage MapChatMessage(AgentContextMessage message)
        => new(MapRole(message.Role), message.Text);

    private static AgentContextMessageRole MapRole(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return AgentContextMessageRole.User;
        }

        if (role == ChatRole.Assistant)
        {
            return AgentContextMessageRole.Assistant;
        }

        return AgentContextMessageRole.System;
    }

    private static ChatRole MapRole(AgentContextMessageRole role)
        => role switch
        {
            AgentContextMessageRole.User => ChatRole.User,
            AgentContextMessageRole.Assistant => ChatRole.Assistant,
            AgentContextMessageRole.System => ChatRole.System,
            _ => ChatRole.System
        };
}
