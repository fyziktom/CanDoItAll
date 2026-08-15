using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Llm.ProviderRuntime;

/// <summary>
/// Provider-backed <see cref="ILlmInvocationPort"/> built directly above the existing provider runtime
/// pool/driver boundary (descriptor upsert -&gt; pool handle -&gt; dispatch -&gt; chat-completion driver), mirroring
/// the established dispatch mechanics used by the full agent runtime's own provider test-chat gateway. This
/// adapter never constructs an agent, session, capability graph, or workspace/authority context - it only
/// ever sees provider/model selection, ordered messages, attachments, response format, and model settings.
/// </summary>
public sealed class ProviderBackedLlmInvocationAdapter(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool) : ILlmInvocationPort
{
    /// <summary>One stateless retry for intermittent empty terminal responses.</summary>
    public const int MaximumEmptyResponseAttempts = 2;

    public async Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = request.Provider;
        var model = ResolveModel(provider, request.Model);
        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout is { } timeout)
        {
            deadlineCancellation.CancelAfter(timeout);
        }

        LlmUsage? aggregateUsage = null;
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                ProviderChatCompletionResult result;
                try
                {
                    result = await DispatchAsync(request, provider, model, deadlineCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Sanitized typed failure: stable identifiers only. The raw
                    // provider exception stays as the inner exception for
                    // structured logging and never reaches user-facing text.
                    throw new LlmInvocationException(
                        LlmInvocationFailureKind.ProviderFailure,
                        provider.Name,
                        model,
                        request.CorrelationId,
                        exception,
                        aggregateUsage);
                }

                try
                {
                    var attemptUsage = new LlmUsage(
                        result.InputTokens, result.OutputTokens, result.CachedInputTokens);
                    aggregateUsage = aggregateUsage is null
                        ? attemptUsage
                        : aggregateUsage.Add(attemptUsage);
                }
                catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
                {
                    throw new LlmInvocationException(
                        LlmInvocationFailureKind.ProviderFailure,
                        provider.Name,
                        model,
                        request.CorrelationId,
                        exception,
                        aggregateUsage);
                }

                if (!string.IsNullOrWhiteSpace(result.ResponseText))
                {
                    return new LlmInvocationResult(
                        result.Model,
                        result.ResponseText,
                        aggregateUsage);
                }

                if (attempt >= MaximumEmptyResponseAttempts)
                {
                    throw new LlmInvocationException(
                        LlmInvocationFailureKind.EmptyResponse,
                        provider.Name,
                        model,
                        request.CorrelationId,
                        usage: aggregateUsage);
                }
            }
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested && deadlineCancellation.IsCancellationRequested)
        {
            throw new LlmInvocationException(
                LlmInvocationFailureKind.DeadlineExceeded,
                provider.Name,
                model,
                request.CorrelationId,
                exception,
                aggregateUsage);
        }
    }

    private async Task<ProviderChatCompletionResult> DispatchAsync(
        LlmInvocationRequest request,
        ProviderProfile provider,
        string model,
        CancellationToken cancellationToken)
    {
        var handle = await GetRuntimeHandleAsync(provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            model);
        var payload = CreateProviderRequest(request, provider, model);
        return await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderChatCompletionRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderChatCompletionDriver>(context.Query.Provider.Kind);
                return await driver.CompleteChatAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal static ProviderChatCompletionRequest CreateProviderRequest(
        LlmInvocationRequest request,
        ProviderProfile provider,
        string model)
        => new(
            provider,
            model,
            BuildSystemPrompt(request.Messages),
            BuildPriorTurns(request.Messages),
            BuildFinalUserPrompt(request.Messages),
            BuildAttachments(request.Attachments),
            ResolveModelParameterConfiguration(request.Settings),
            request.Settings?.Temperature,
            BuildResponseFormat(request.ResponseFormat));

    private async ValueTask<IProviderRuntimeHandle> GetRuntimeHandleAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        descriptorStore.Upsert(provider, secretReferenceIdentity: provider.ApiKeyEnvironmentVariable);
        return await runtimePool.GetRequiredAsync(provider.Id, cancellationToken).ConfigureAwait(false);
    }

    internal static void EnsureProviderKindMatches(
        ProviderRuntimeDescriptor descriptor,
        ProviderProfile provider)
    {
        if (descriptor.ProviderKind != provider.Kind)
        {
            throw new InvalidOperationException("Provider runtime descriptor kind does not match the request provider kind.");
        }
    }

    internal static string ResolveModel(
        ProviderProfile provider,
        string requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel.Trim();
        }

        return provider.SuggestedModels.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim()
            ?? string.Empty;
    }

    /// <summary>
    /// The first System message text; when several System messages are present, they are concatenated with a
    /// blank line so no instruction content is dropped.
    /// </summary>
    private static string BuildSystemPrompt(IReadOnlyList<LlmMessage> messages)
    {
        var systemTexts = messages
            .Where(message => message.Role == LlmMessageRole.System && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text.Trim())
            .ToArray();
        return string.Join("\n\n", systemTexts);
    }

    /// <summary>
    /// Every User/Assistant message in order, excluding the last User message (which becomes the driver
    /// request's dedicated <see cref="ProviderChatCompletionRequest.Prompt"/>). Timestamps are synthesized as a
    /// strictly increasing sequence so the driver's own CreatedAtUtc ordering never reorders the conversation.
    /// </summary>
    private static IReadOnlyList<ProviderTestChatMessage> BuildPriorTurns(IReadOnlyList<LlmMessage> messages)
    {
        var lastUserIndex = FindLastUserMessageIndex(messages);
        var priorTurns = new List<ProviderTestChatMessage>();
        var sequence = 0L;
        for (var index = 0; index < messages.Count; index++)
        {
            if (index == lastUserIndex)
            {
                continue;
            }

            var message = messages[index];
            if (message.Role is not (LlmMessageRole.User or LlmMessageRole.Assistant) ||
                string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            priorTurns.Add(new ProviderTestChatMessage(
                MapRole(message.Role),
                message.Text.Trim(),
                DateTimeOffset.UnixEpoch.AddTicks(sequence++)));
        }

        return priorTurns;
    }

    private static string BuildFinalUserPrompt(IReadOnlyList<LlmMessage> messages)
    {
        var lastUserIndex = FindLastUserMessageIndex(messages);
        return lastUserIndex >= 0 ? messages[lastUserIndex].Text.Trim() : string.Empty;
    }

    private static int FindLastUserMessageIndex(IReadOnlyList<LlmMessage> messages)
    {
        for (var index = messages.Count - 1; index >= 0; index--)
        {
            if (messages[index].Role == LlmMessageRole.User)
            {
                return index;
            }
        }

        return -1;
    }

    private static ChatMessageRole MapRole(LlmMessageRole role)
        => role switch
        {
            LlmMessageRole.Assistant => ChatMessageRole.Assistant,
            _ => ChatMessageRole.User
        };

    private static IReadOnlyList<ProviderChatAttachment>? BuildAttachments(IReadOnlyList<LlmAttachment> attachments)
        => attachments.Count == 0
            ? null
            : attachments.Select(attachment => new ProviderChatAttachment(
                attachment.Name,
                attachment.ContentType,
                [.. attachment.Bytes])).ToArray();

    private static ProviderChatResponseFormat? BuildResponseFormat(LlmResponseFormat? responseFormat)
        => responseFormat is null
            ? null
            : new ProviderChatResponseFormat(
                responseFormat.RequireJson,
                responseFormat.SchemaJson,
                responseFormat.SchemaName,
                responseFormat.SchemaDescription);

    private static string ResolveModelParameterConfiguration(LlmModelSettings? settings)
    {
        if (settings is null)
        {
            return string.Empty;
        }

        return settings.ThinkingEffort is { } thinkingEffort
            ? AgentThinkingEffortPolicy.WriteAgentOverride(
                settings.ModelParameterConfigurationJson,
                thinkingEffort)
            : settings.ModelParameterConfigurationJson;
    }
}
