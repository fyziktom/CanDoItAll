using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeResponseAssembler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static AgentResponse ProjectTerminalResponse(
        AgentResponse activityResponse,
        AgentResponseUpdate? terminalResponseUpdate)
    {
        ArgumentNullException.ThrowIfNull(activityResponse);

        if (terminalResponseUpdate is null)
        {
            return activityResponse;
        }

        var response = new[] { terminalResponseUpdate }.ToAgentResponse();
        response.AdditionalProperties ??= activityResponse.AdditionalProperties;
        response.AgentId ??= activityResponse.AgentId;
        response.ContinuationToken ??= activityResponse.ContinuationToken;
        response.CreatedAt ??= activityResponse.CreatedAt;
        response.FinishReason ??= activityResponse.FinishReason;
        response.ResponseId ??= activityResponse.ResponseId;
        response.Usage ??= activityResponse.Usage;
        return response;
    }

    public static AgentRuntimeResponse? TryBuildRequiredFinalizerRuntimeResponse(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        string runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var finalizerValidation = new DefaultAgentFinalizerValidator().Validate(policy, finalizerInvocations);
        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            return null;
        }

        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, toolInvocationTraces);
        if (!sequenceValidation.Succeeded)
        {
            return null;
        }

        return new AgentRuntimeResponse(
            JsonSerializer.Serialize(finalizerValidation.Output, policy.OutputType, AgentOutputJson.SerializerOptions),
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: toolInvocationTraces
                .Where(trace => !string.IsNullOrWhiteSpace(trace.ToolName))
                .Select(trace => $"{trace.ToolName}|{trace.Sequence}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RuntimeSessionKey: runtimeSessionKey,
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = usageObservations
        };
    }

    public static IReadOnlyList<ProviderUsageObservation> CreateProviderUsageObservations(
        ProviderProfile provider,
        string model,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        IReadOnlyList<AgentResponseUpdate> updates,
        string sourcePhase,
        string diagnostic)
    {
        if (updates.Count == 0)
        {
            return
            [
                CreateMissingProviderUsageObservation(
                    provider,
                    model,
                    ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
                    sourcePhase,
                    diagnostic)
            ];
        }

        var usageUpdateGroups = GroupUsageUpdatesByProviderResponse(updates);
        if (usageUpdateGroups.Count == 0)
        {
            return [CreateProviderUsageObservation(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates.ToAgentResponse(),
                sourcePhase,
                diagnostic)];
        }

        return usageUpdateGroups
            .Select(group => CreateProviderUsageObservation(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                group.ToAgentResponse(),
                sourcePhase,
                diagnostic))
            .ToList();
    }

    private static ProviderUsageObservation CreateProviderUsageObservation(
        ProviderProfile provider,
        string model,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentResponse response,
        string sourcePhase,
        string diagnostic)
    {
        var runtimeKey = ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey);
        var rawUsageJson = response.Usage is null
            ? string.Empty
            : JsonSerializer.Serialize(response.Usage, SerializerOptions);

        return DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
            Provider: provider,
            Model: model,
            SourcePhase: sourcePhase,
            UsageStatus: response.Usage is null
                ? ProviderUsageObservationStatus.UsageUnavailable
                : ProviderUsageObservationStatus.Observed,
            InputTokens: ClampTokenCount(response.Usage?.InputTokenCount),
            CachedInputTokens: ClampTokenCount(response.Usage?.CachedInputTokenCount),
            OutputTokens: ClampTokenCount(response.Usage?.OutputTokenCount),
            ReasoningTokens: 0,
            TotalTokens: ClampTokenCount(response.Usage?.TotalTokenCount),
            ToolCallCount: CountToolCalls(response),
            ProviderResponseId: response.ResponseId ?? response.ContinuationToken?.ToString() ?? string.Empty,
            ProviderRequestId: string.Empty,
            RuntimeSessionKey: runtimeKey,
            RawUsageJson: rawUsageJson,
            DiagnosticsJson: JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["diagnostic"] = diagnostic
                },
                SerializerOptions)));
    }

    private static IReadOnlyList<IReadOnlyList<AgentResponseUpdate>> GroupUsageUpdatesByProviderResponse(
        IReadOnlyList<AgentResponseUpdate> updates)
    {
        var indexedUpdates = updates
            .Select((update, index) => (Update: update, Index: index))
            .ToArray();
        var groups = indexedUpdates
            .Where(item => HasUsage(item.Update) && !string.IsNullOrWhiteSpace(item.Update.ResponseId))
            .Select(item => item.Update.ResponseId!)
            .Distinct(StringComparer.Ordinal)
            .Select(responseId =>
            {
                var responseUpdates = indexedUpdates
                    .Where(item => string.Equals(item.Update.ResponseId, responseId, StringComparison.Ordinal))
                    .ToArray();
                return (
                    FirstIndex: responseUpdates.Min(item => item.Index),
                    Updates: (IReadOnlyList<AgentResponseUpdate>)responseUpdates.Select(item => item.Update).ToArray());
            })
            .Concat(indexedUpdates
                .Where(item => HasUsage(item.Update) && string.IsNullOrWhiteSpace(item.Update.ResponseId))
                .Select(item => (
                    FirstIndex: item.Index,
                    Updates: (IReadOnlyList<AgentResponseUpdate>)[item.Update])))
            .OrderBy(group => group.FirstIndex)
            .Select(group => group.Updates)
            .ToList();

        return groups;
    }

    private static bool HasUsage(AgentResponseUpdate update)
    {
        return update.Contents.OfType<UsageContent>().Any();
    }

    public static string BuildProviderFailureDiagnostic(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder("Provider transport failed before a successful runtime response.");
        var current = exception;
        var depth = 0;
        while (current is not null && depth < 4)
        {
            builder
                .Append(" FailureType")
                .Append(depth)
                .Append('=')
                .Append(current.GetType().FullName ?? current.GetType().Name);
            if (current is HttpRequestException { StatusCode: { } statusCode })
            {
                builder
                    .Append("; HttpStatus=")
                    .Append((int)statusCode);
            }

            builder.Append('.');

            current = current.InnerException;
            depth++;
        }

        return builder.ToString();
    }

    public static int ClampTokenCount(long? tokenCount)
    {
        if (!tokenCount.HasValue || tokenCount.Value <= 0)
        {
            return 0;
        }

        return tokenCount.Value > int.MaxValue
            ? int.MaxValue
            : (int)tokenCount.Value;
    }

    public static void ThrowIfEmptyProviderCompletion(
        ProviderProfile provider,
        string model,
        AgentResponse response,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        if (!string.IsNullOrWhiteSpace(response.Text) ||
            pendingApprovals.Count > 0)
        {
            return;
        }

        var outputTokenCount = ClampTokenCount(response.Usage?.OutputTokenCount);
        if (response.FinishReason == ChatFinishReason.Length)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' model '{model}' exhausted its output-token budget after reporting {outputTokenCount} output tokens without a usable terminal answer or pending tool approval. Lower the thinking effort or reduce the prompt and tool context. If this run used a lower explicit maximum, increase it up to the supported limit.");
        }

        if (outputTokenCount > 0)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' model '{model}' reported {outputTokenCount} output tokens, but the terminal completion contained only non-actionable reasoning or metadata and no pending tool approval.");
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' model '{model}' completed without a usable terminal answer or pending tool approval.");
    }

    public static bool ShouldContinueBackgroundRun(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentResponse response,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests)
    {
        if (approvalRequests.Count > 0)
        {
            return false;
        }

        return agent.EnableBackgroundResponses
            && MafRuntimeSessionBuilder.SupportsBackgroundResponses(provider)
            && response.ContinuationToken is not null;
    }

    public static int CountToolCalls(AgentResponse response)
    {
        return response.Messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                ToolApprovalRequestContent approval => approval.ToolCall?.CallId ?? approval.ToolCall?.ToString(),
                ToolCallContent toolCall => toolCall.CallId ?? MafToolInvocationArgumentFormatter.ResolveToolName(toolCall),
                _ => null
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    public static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        AgentResponse response,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return response.ResponseId
            ?? response.ContinuationToken?.ToString()
            ?? fallbackValue
            ?? string.Empty;
    }

    public static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return fallbackValue ?? string.Empty;
    }

    private static ProviderUsageObservation CreateMissingProviderUsageObservation(
        ProviderProfile provider,
        string model,
        string runtimeSessionKey,
        string sourcePhase,
        string diagnostic)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: model,
            TransportKind: provider.Transport,
            SourcePhase: sourcePhase,
            UsageStatus: ProviderUsageObservationStatus.MissingAfterProviderActivity,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0)
        {
            ProviderProfileId = provider.Id,
            RuntimeSessionKey = runtimeSessionKey,
            DiagnosticsJson = JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["diagnostic"] = diagnostic
                },
                SerializerOptions)
        };
    }
}
