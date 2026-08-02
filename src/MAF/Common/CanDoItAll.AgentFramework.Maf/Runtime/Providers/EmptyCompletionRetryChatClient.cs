using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class EmptyCompletionRetryChatClient(
    IChatClient innerClient,
    ProviderProfile provider,
    string model,
    bool allowBackgroundResponses,
    ILogger? logger) : DelegatingChatClient(innerClient)
{
    private const int MaximumProviderAttempts = 2;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materializedMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var firstResponse = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
        if (!IsRetryableNonActionableCompletion(firstResponse))
        {
            return firstResponse;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (allowBackgroundResponses)
        {
            Report(1, ProviderEmptyCompletionOutcome.SuppressedBackground);
            return firstResponse;
        }

        if (HasProviderExecutedTools(options))
        {
            Report(1, ProviderEmptyCompletionOutcome.SuppressedUnsafeTools);
            return firstResponse;
        }

        Report(1, ProviderEmptyCompletionOutcome.Retrying);

        var secondResponse = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
        MergeUsage(firstResponse, secondResponse);
        Report(
            2,
            HasActionableOutput(secondResponse)
                ? ProviderEmptyCompletionOutcome.Recovered
                : ProviderEmptyCompletionOutcome.Exhausted);
        return secondResponse;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materializedMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var bufferedUpdates = new List<ChatResponseUpdate>();
        var firstAttemptIsRetryable = true;

        await foreach (var update in base
                           .GetStreamingResponseAsync(materializedMessages, options, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            if (!firstAttemptIsRetryable)
            {
                yield return update;
                continue;
            }

            bufferedUpdates.Add(update);
            if (IsPotentiallyRetryableUpdate(update))
            {
                continue;
            }

            firstAttemptIsRetryable = false;
            foreach (var bufferedUpdate in bufferedUpdates)
            {
                yield return bufferedUpdate;
            }

            bufferedUpdates.Clear();
        }

        if (!firstAttemptIsRetryable)
        {
            yield break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (allowBackgroundResponses)
        {
            Report(1, ProviderEmptyCompletionOutcome.SuppressedBackground);
            foreach (var bufferedUpdate in bufferedUpdates)
            {
                yield return bufferedUpdate;
            }

            yield break;
        }

        if (HasProviderExecutedTools(options))
        {
            Report(1, ProviderEmptyCompletionOutcome.SuppressedUnsafeTools);
            foreach (var bufferedUpdate in bufferedUpdates)
            {
                yield return bufferedUpdate;
            }

            yield break;
        }

        Report(1, ProviderEmptyCompletionOutcome.Retrying);

        var firstAttemptUsage = AggregateUsage(bufferedUpdates);
        if (firstAttemptUsage is not null)
        {
            yield return new ChatResponseUpdate(
                role: null,
                contents: [new UsageContent(firstAttemptUsage)]);
        }

        var secondAttemptHasActionableOutput = false;
        await foreach (var update in base
                           .GetStreamingResponseAsync(materializedMessages, options, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            secondAttemptHasActionableOutput |= HasActionableOutput(update);
            yield return update;
        }

        Report(
            2,
            secondAttemptHasActionableOutput
                ? ProviderEmptyCompletionOutcome.Recovered
                : ProviderEmptyCompletionOutcome.Exhausted);
    }

    private static bool IsRetryableNonActionableCompletion(ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!string.IsNullOrWhiteSpace(response.Text) ||
            response.ContinuationToken is not null ||
            !IsRetryableFinishReason(response.FinishReason))
        {
            return false;
        }

        return response.Messages
            .SelectMany(message => message.Contents)
            .All(IsNonMaterialContent);
    }

    private static bool IsPotentiallyRetryableUpdate(ChatResponseUpdate update)
    {
        return update.ContinuationToken is null &&
               IsRetryableFinishReason(update.FinishReason) &&
               update.Contents.All(IsNonMaterialContent);
    }

    private static bool HasActionableOutput(ChatResponse response)
    {
        return !string.IsNullOrWhiteSpace(response.Text) ||
               response.ContinuationToken is not null ||
               response.Messages
                   .SelectMany(message => message.Contents)
                   .Any(content => !IsNonMaterialContent(content));
    }

    private static bool HasActionableOutput(ChatResponseUpdate update)
    {
        return update.ContinuationToken is not null ||
               update.Contents.Any(content => !IsNonMaterialContent(content));
    }

    private static bool IsRetryableFinishReason(ChatFinishReason? finishReason)
    {
        return finishReason is null || finishReason == ChatFinishReason.Stop;
    }

    private static bool HasProviderExecutedTools(ChatOptions? options)
    {
        return options?.Tools?.Any(tool => tool is not AIFunction) == true;
    }

    private static bool IsNonMaterialContent(AIContent content)
    {
        return content switch
        {
            TextContent text => string.IsNullOrWhiteSpace(text.Text),
            TextReasoningContent => true,
            UsageContent => true,
            _ => false
        };
    }

    private static UsageDetails? AggregateUsage(IEnumerable<ChatResponseUpdate> updates)
    {
        UsageDetails? aggregate = null;
        foreach (var usage in updates
                     .SelectMany(update => update.Contents)
                     .OfType<UsageContent>()
                     .Select(content => content.Details))
        {
            aggregate ??= new UsageDetails();
            aggregate.Add(usage);
        }

        return aggregate;
    }

    private static void MergeUsage(ChatResponse firstResponse, ChatResponse secondResponse)
    {
        if (firstResponse.Usage is null)
        {
            return;
        }

        var aggregate = new UsageDetails();
        aggregate.Add(firstResponse.Usage);
        if (secondResponse.Usage is not null)
        {
            aggregate.Add(secondResponse.Usage);
        }

        secondResponse.Usage = aggregate;
    }

    private void Report(int attempt, ProviderEmptyCompletionOutcome outcome)
    {
        AgentFrameworkTelemetry.RecordProviderEmptyCompletion(provider, model, attempt, outcome);

        switch (outcome)
        {
            case ProviderEmptyCompletionOutcome.Retrying:
                logger?.LogWarning(
                    "Provider {ProviderName} model {Model} transport {Transport} returned a non-actionable completion on attempt {Attempt}/{MaximumAttempts}; retrying once before any tool execution.",
                    provider.Name,
                    model,
                    provider.Transport,
                    attempt,
                    MaximumProviderAttempts);
                break;
            case ProviderEmptyCompletionOutcome.Recovered:
                logger?.LogWarning(
                    "Provider {ProviderName} model {Model} transport {Transport} recovered from a non-actionable completion on attempt {Attempt}/{MaximumAttempts}.",
                    provider.Name,
                    model,
                    provider.Transport,
                    attempt,
                    MaximumProviderAttempts);
                break;
            case ProviderEmptyCompletionOutcome.Exhausted:
                logger?.LogWarning(
                    "Provider {ProviderName} model {Model} transport {Transport} returned non-actionable completions for {Attempt}/{MaximumAttempts} attempts; the terminal runtime guard will reject the response.",
                    provider.Name,
                    model,
                    provider.Transport,
                    attempt,
                    MaximumProviderAttempts);
                break;
            case ProviderEmptyCompletionOutcome.SuppressedUnsafeTools:
                logger?.LogWarning(
                    "Provider {ProviderName} model {Model} transport {Transport} returned a non-actionable completion on attempt {Attempt}/{MaximumAttempts}; retry was suppressed because the request included provider-executed or unknown tools.",
                    provider.Name,
                    model,
                    provider.Transport,
                    attempt,
                    MaximumProviderAttempts);
                break;
            case ProviderEmptyCompletionOutcome.SuppressedBackground:
                logger?.LogWarning(
                    "Provider {ProviderName} model {Model} transport {Transport} returned a non-actionable completion on attempt {Attempt}/{MaximumAttempts}; retry was suppressed because background responses are enabled for this agent.",
                    provider.Name,
                    model,
                    provider.Transport,
                    attempt,
                    MaximumProviderAttempts);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown provider empty-completion outcome.");
        }
    }
}
