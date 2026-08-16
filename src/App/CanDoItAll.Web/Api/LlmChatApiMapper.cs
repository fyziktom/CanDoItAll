using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Web.Api;

internal static class LlmChatApiMapper
{
    public static bool TryMapCreate(
        LlmChatDefinitionMutationApiRequest request,
        out CreateLlmChatDefinitionCommand? command,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryMapSettings(request, out var settings, out var timeout, out var responseFormat, out error))
        {
            command = null;
            return false;
        }

        command = new CreateLlmChatDefinitionCommand(
            request.Name,
            request.Summary,
            request.AvatarImageUrl,
            request.SystemPrompt,
            request.ProviderProfileId,
            request.Model,
            settings!,
            timeout,
            responseFormat,
            request.RevisionReason,
            request.Tags);
        return true;
    }

    public static bool TryMapUpdate(
        LlmChatDefinitionId definitionId,
        LlmChatDefinitionMutationApiRequest request,
        long expectedConcurrencyToken,
        out UpdateLlmChatDefinitionCommand? command,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryMapSettings(request, out var settings, out var timeout, out var responseFormat, out error))
        {
            command = null;
            return false;
        }

        command = new UpdateLlmChatDefinitionCommand(
            definitionId,
            request.Name,
            request.Summary,
            request.AvatarImageUrl,
            request.SystemPrompt,
            request.ProviderProfileId,
            request.Model,
            settings!,
            timeout,
            responseFormat,
            request.RevisionReason,
            expectedConcurrencyToken,
            request.Tags);
        return true;
    }

    public static LlmChatDefinitionApiResponse ToListResponse(LlmChatDefinitionDetails details)
        => ToResponse(details, includeEditorFields: false);

    public static LlmChatDefinitionApiResponse ToDetailResponse(LlmChatDefinitionDetails details)
        => ToResponse(details, includeEditorFields: true);

    public static LlmChatDefinitionEditorApiResponse ToEditorResponse(LlmChatDefinitionDetails details)
        => new(
            details.Definition.Id.Value,
            details.Definition.Name,
            details.Definition.Summary,
            details.Definition.AvatarImageUrl,
            details.Definition.Status,
            details.Revision.Revision.Value,
            details.Revision.SystemPrompt,
            details.Revision.ProviderProfileId,
            details.Revision.ProviderName,
            details.Revision.ProviderKind,
            details.Revision.Model,
            details.Revision.Settings.ThinkingEffort,
            new LlmChatModelSettingsApiResponse(
                details.Revision.Settings.Temperature,
                ParseJson(details.Revision.Settings.ModelParameterConfigurationJson, JsonValueKind.Object),
                details.Revision.Timeout?.TotalSeconds),
            details.Revision.ResponseFormat is { } format
                ? new LlmChatResponseFormatApiResponse(
                    format.RequireJson,
                    ParseJson(format.SchemaJson, JsonValueKind.Object),
                    format.SchemaName,
                    format.SchemaDescription)
                : null,
            details.NormalizedTags,
            details.Revision.Reason,
            details.Definition.ConcurrencyToken,
            details.Definition.CreatedAtUtc,
            details.Definition.UpdatedAtUtc);

    public static LlmChatConversationApiResponse ToResponse(LlmChatConversationDetails details)
    {
        var response = new LlmChatConversationApiResponse(
            details.Conversation.Id.Value,
            details.Conversation.DefinitionId.Value,
            details.Conversation.DefinitionRevision.Value,
            details.DefinitionName,
            details.Conversation.Title,
            details.Conversation.Status,
            details.Conversation.Origin,
            details.Transcript.TranscriptRevision,
            details.Transcript.HasActiveTurn,
            details.Conversation.ConcurrencyToken,
            details.Conversation.CreatedAtUtc,
            details.Conversation.UpdatedAtUtc)
        {
            ActiveOperationId = details.ActiveOperationId?.Value
        };
        if (details.Messages is null)
        {
            return response;
        }

        return response with
        {
            Messages = [.. details.TranscriptMessages.Select(message => new LlmChatMessageApiResponse(
                message.EntryId,
                message.TurnId,
                message.Role,
                message.Text,
                message.CreatedAtUtc,
                message.Model,
                message.Usage is { } usage
                    ? new LlmChatUsageApiResponse(
                        usage.InputTokens,
                        usage.OutputTokens,
                        usage.CachedInputTokens)
                    : null))],
            NextMessageCursor = details.NextMessageCursor is { } next
                ? LlmChatApiCursorCodec.Encode(next)
                : null
        };
    }

    public static LlmChatProviderOptionApiResponse ToResponse(LlmChatProviderOption option)
        => new(
            option.ProviderProfileId,
            option.ProviderName,
            option.ProviderKind,
            [.. option.Models.Select(model => new LlmChatModelOptionApiResponse(
                model.Model,
                new LlmChatThinkingEffortOptionApiResponse(
                    model.ThinkingEffort.Status,
                    model.ThinkingEffort.ControlMode,
                    model.ThinkingEffort.AllowedEfforts,
                    model.ThinkingEffort.ProviderDefault))) ]);

    private static LlmChatDefinitionApiResponse ToResponse(
        LlmChatDefinitionDetails details,
        bool includeEditorFields)
    {
        var response = new LlmChatDefinitionApiResponse(
            details.Definition.Id.Value,
            details.Definition.Name,
            details.Definition.Summary,
            details.Definition.AvatarImageUrl,
            details.Definition.Status,
            details.Definition.CurrentRevision.Value,
            details.Revision.ProviderProfileId,
            details.Revision.ProviderName,
            details.Revision.ProviderKind,
            details.Revision.Model,
            details.Revision.Settings.ThinkingEffort,
            details.NormalizedTags,
            details.Definition.ConcurrencyToken,
            details.Definition.CreatedAtUtc,
            details.Definition.UpdatedAtUtc);
        if (!includeEditorFields)
        {
            return response;
        }

        return response with
        {
            ModelSettings = new LlmChatModelSettingsApiResponse(
                details.Revision.Settings.Temperature,
                ParseJson(details.Revision.Settings.ModelParameterConfigurationJson, JsonValueKind.Object),
                details.Revision.Timeout?.TotalSeconds),
            ResponseFormat = details.Revision.ResponseFormat is { } format
                ? new LlmChatResponseFormatApiResponse(
                    format.RequireJson,
                    ParseJson(format.SchemaJson, JsonValueKind.Object),
                    format.SchemaName,
                    format.SchemaDescription)
                : null,
            RevisionReason = details.Revision.Reason
        };
    }

    private static bool TryMapSettings(
        LlmChatDefinitionMutationApiRequest request,
        out LlmModelSettings? settings,
        out TimeSpan? timeout,
        out LlmResponseFormat? responseFormat,
        out string error)
    {
        settings = null;
        timeout = null;
        responseFormat = null;
        error = string.Empty;
        var modelSettings = request.ModelSettings;
        var parameters = modelSettings?.ModelParameterConfiguration ?? default;
        if (parameters.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null and not JsonValueKind.Object)
        {
            error = "Model parameter configuration must be a JSON object.";
            return false;
        }

        if (parameters.ValueKind == JsonValueKind.Object && ContainsThinkingEffort(parameters))
        {
            error = "Thinking effort must be supplied only through the typed thinkingEffort field.";
            return false;
        }

        if (modelSettings?.TimeoutSeconds is { } timeoutSeconds)
        {
            if (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0)
            {
                error = "Timeout seconds must be a finite positive number.";
                return false;
            }

            try
            {
                timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }
            catch (OverflowException)
            {
                error = "Timeout seconds are out of range.";
                return false;
            }
        }

        var parameterJson = parameters.ValueKind == JsonValueKind.Object
            ? parameters.GetRawText()
            : "{}";
        settings = new LlmModelSettings(modelSettings?.Temperature, parameterJson)
        {
            ThinkingEffort = request.ThinkingEffort
        };

        if (request.ResponseFormat is { } format)
        {
            if (format.Schema.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null and not JsonValueKind.Object)
            {
                error = "A response schema must be a JSON object.";
                return false;
            }

            responseFormat = new LlmResponseFormat(
                format.RequireJson,
                format.Schema.ValueKind == JsonValueKind.Object ? format.Schema.GetRawText() : string.Empty,
                format.SchemaName,
                format.SchemaDescription);
        }

        return true;
    }

    private static bool ContainsThinkingEffort(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, "thinkingEffort", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    property.Name,
                    AgentThinkingEffortPolicy.ReasoningEffortConfigurationPropertyName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "think", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.Object && ContainsThinkingEffort(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonElement ParseJson(string json, JsonValueKind requiredKind)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonDocument.Parse(requiredKind == JsonValueKind.Object ? "{}" : "null").RootElement.Clone();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
