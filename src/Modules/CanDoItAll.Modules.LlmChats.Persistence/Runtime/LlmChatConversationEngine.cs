using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class LlmChatConversationEngine(
    ILlmConversationService conversationService,
    ILlmInvocationPort invocationPort,
    ILlmChatConversationReadStore readStore,
    CanonicalLlmChatProviderResolver providerResolver,
    ILlmChatRuntimeLeaseFactory runtimeLeaseFactory,
    ILlmChatOperationScopeAccessor operationScope) : ILlmChatConversationEngine
{
    private readonly ILlmChatProviderExecutionResolver executionResolver = providerResolver;

    public Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            LlmChatOperationId.New(),
            async token =>
            {
                var resolution = await ResolveProviderAsync(definitionRevision, token).ConfigureAwait(false);
                var document = await conversationService.StartAsync(
                    new LlmConversationStartRequest(
                        resolution.Profile,
                        resolution.Resolved.Model,
                        title,
                        definitionRevision.SystemPrompt)
                    {
                        ConversationId = conversationId.Value
                    },
                    token).ConfigureAwait(false);
                return Map(document);
            },
            cancellationToken);

    public Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            LlmChatOperationId.New(),
            async token =>
            {
                var conversation = await readStore.TryGetAsync(conversationId, token)
                    .ConfigureAwait(false);
                return conversation?.Transcript;
            },
            cancellationToken);

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        return ExecuteAsync(
            LlmChatOperationId.New(),
            async token =>
            {
                var page = await readStore.TryGetTranscriptPageAsync(conversationId, take, cursor, token)
                    .ConfigureAwait(false);
                if (page is null)
                {
                    return null;
                }

                return new LlmChatTranscriptPage(
                    page.Conversation.Transcript,
                    page.Entries,
                    page.NextCursor);
            },
            cancellationToken);
    }

    public Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            LlmChatOperationId.New(),
            async token => Map(await conversationService.RenameAsync(
                conversationId.Value,
                title,
                expectedTranscriptRevision,
                token).ConfigureAwait(false)),
            cancellationToken);

    public Task<LlmChatConversationEngineTurnResult> SendAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(
            conversationId,
            operationId,
            definition,
            definitionRevision,
            userText,
            expectedTranscriptRevision,
            cancellationToken);

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definitionRevision);
        return ExecuteAsync(
            operationId,
            async token =>
            {
                EnsureExecutableDefinition(definition, definitionRevision);
                EnsureTypedThinkingEffortIsAuthoritative(definitionRevision.Settings);
                var resolution = await ResolveProviderAsync(definitionRevision, token).ConfigureAwait(false);
                return await conversationService.AdmitTurnAsync(
                    new LlmConversationTurnRequest(
                        conversationId.Value,
                        expectedTranscriptRevision,
                        userText,
                        resolution.Profile,
                        resolution.Resolved.Model,
                        LlmConversationProviderChangePolicy.Forbid,
                        definitionRevision.ResponseFormat,
                        definitionRevision.Settings,
                        definitionRevision.Timeout,
                        operationId.ToString())
                    {
                        TurnId = operationId.ToTurnId()
                    },
                    token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<LlmInvocationResult> InvokeTurnAsync(
        LlmConversationTurnAdmission admission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return ExecuteAsync(
            new LlmChatOperationId(admission.UserEntry.TurnId),
            token => invocationPort.InvokeAsync(admission.InvocationRequest, token),
            cancellationToken);
    }

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definitionRevision);
        return ExecuteAsync(
            operationId,
            async token =>
            {
                EnsureExecutableDefinition(definition, definitionRevision);
                EnsureTypedThinkingEffortIsAuthoritative(definitionRevision.Settings);
                var resolution = await ResolveProviderAsync(definitionRevision, token).ConfigureAwait(false);
                return await conversationService.ResumeAdmittedTurnAsync(
                    new LlmConversationAdmittedTurnRequest(
                        conversationId.Value,
                        operationId.Value,
                        resolution.Profile,
                        resolution.Resolved.Model,
                        definitionRevision.ResponseFormat,
                        definitionRevision.Settings,
                        definitionRevision.Timeout,
                        operationId.ToString()),
                    token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(invocationResult);
        var operationId = new LlmChatOperationId(admission.UserEntry.TurnId);
        return ExecuteAsync(
            operationId,
            async token =>
            {
                var result = await conversationService.CompleteTurnAsync(admission, invocationResult, token)
                    .ConfigureAwait(false);
                return new LlmChatConversationEngineTurnResult(
                    Map(result.Conversation),
                    operationId,
                    result.AssistantEntry.EntryId,
                    result.AssistantEntry.Text,
                    result.AssistantEntry.Model,
                    result.AssistantEntry.Usage ?? LlmUsage.Zero);
            },
            cancellationToken);
    }

    public Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            operationId,
            async token => Map(await conversationService.CompensateTurnAsync(
                conversationId.Value,
                operationId.Value,
                token).ConfigureAwait(false)),
            cancellationToken);

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            operationId,
            token => readStore.TryInspectTurnAsync(conversationId, operationId, token),
            cancellationToken);

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            operationId,
            async token => Map(await conversationService.AbandonActiveTurnAsync(
                conversationId.Value,
                operationId.Value,
                token).ConfigureAwait(false)),
            cancellationToken);

    private async Task<LlmChatConversationEngineTurnResult> SendCoreAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken)
    {
        var admission = await AdmitTurnAsync(
            conversationId,
            operationId,
            definition,
            definitionRevision,
            userText,
            expectedTranscriptRevision,
            cancellationToken).ConfigureAwait(false);
        try
        {
            var invocationResult = await InvokeTurnAsync(admission, cancellationToken).ConfigureAwait(false);
            return await CompleteTurnAsync(admission, invocationResult, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CompensateTurnAsync(conversationId, operationId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<T> ExecuteAsync<T>(
        LlmChatOperationId operationId,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        if (operationScope.Current is not null)
        {
            return await execute(cancellationToken).ConfigureAwait(false);
        }

        await using var lease = await runtimeLeaseFactory.AcquireAsync(cancellationToken).ConfigureAwait(false);
        EnsureCurrent(lease);
        using var scope = operationScope.Push(new LlmChatOperationExecutionContext(operationId, lease.Identity));
        try
        {
            var result = await execute(lease.CancellationToken).ConfigureAwait(false);
            EnsureCurrent(lease);
            return result;
        }
        catch (OperationCanceledException) when (lease.EnsureCurrent().IsFailure)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }

    private async Task<LlmChatProviderExecutionResolution> ResolveProviderAsync(
        LlmChatDefinitionRevision revision,
        CancellationToken cancellationToken)
    {
        var resolution = await executionResolver.ResolveExecutionAsync(
            revision.ProviderProfileId,
            revision.ProviderKind,
            revision.Model,
            revision.Settings.ThinkingEffort,
            cancellationToken).ConfigureAwait(false);
        if (resolution.IsSuccess)
        {
            return resolution.Value!;
        }

        var error = resolution.Errors.First();
        throw new LlmChatConversationEngineException(error.Code, error.Message);
    }

    private static void EnsureExecutableDefinition(
        LlmChatDefinition definition,
        LlmChatDefinitionRevision revision)
    {
        if (definition.Status != LlmChatDefinitionStatus.Active)
        {
            throw new LlmChatConversationEngineException(
                LlmChatErrorCodes.DefinitionNotActive,
                "The LLM Chat definition is not active.");
        }

        if (definition.Id != revision.DefinitionId)
        {
            throw new LlmChatConversationEngineException(
                LlmChatErrorCodes.StorageCorrupted,
                "The pinned LLM Chat definition revision does not belong to the conversation definition.");
        }
    }

    private static void EnsureTypedThinkingEffortIsAuthoritative(LlmModelSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelParameterConfigurationJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(settings.ModelParameterConfigurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                ContainsThinkingEffortProperty(document.RootElement))
            {
                throw new LlmChatConversationEngineException(
                    LlmChatErrorCodes.ModelSettingsInvalid,
                    "Thinking effort must use the typed definition setting, not the model-parameter JSON envelope.");
            }
        }
        catch (JsonException)
        {
            throw new LlmChatConversationEngineException(
                LlmChatErrorCodes.ModelSettingsInvalid,
                "The model-parameter settings must be a valid JSON object.");
        }
    }

    private static bool ContainsThinkingEffortProperty(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    AgentThinkingEffortPolicy.ReasoningEffortConfigurationPropertyName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "think", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    property.Name,
                    AgentThinkingEffortPolicy.ModelParametersConfigurationPropertyName,
                    StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Object &&
                ContainsThinkingEffortProperty(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureCurrent(ILlmChatRuntimeLease lease)
    {
        if (lease.EnsureCurrent().IsFailure)
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }

    private static LlmChatConversationEngineState Map(LlmConversationDocument document)
        => new(
            new LlmChatConversationId(document.ConversationId),
            document.TranscriptRevision,
            document.ActiveTurn is not null,
            document.CreatedAtUtc,
            document.UpdatedAtUtc);
}
