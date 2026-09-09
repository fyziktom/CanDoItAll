using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal static class LlmChatDefinitionEditorForm {
    public static DefinitionEditorValues From(LlmChatDefinitionEditor editor) {
        ArgumentNullException.ThrowIfNull(editor);
        return new() {
            Name = editor.Definition.Name,
            Summary = editor.Definition.Summary,
            AvatarImageUrl = editor.Definition.AvatarImageUrl,
            SystemPrompt = editor.SystemPrompt,
            ProviderProfileId = editor.ProviderProfileId,
            Model = editor.Model,
            Temperature = editor.Temperature,
            ThinkingEffort = ToPresentation(editor.ThinkingEffort),
            ModelParameterConfigurationJson = editor.ModelParameterConfigurationJson,
            TimeoutSeconds = editor.Timeout?.TotalSeconds,
            ResponseFormat = ToPresentation(editor.ResponseFormat),
            SchemaJson = editor.SchemaJson,
            SchemaName = editor.SchemaName,
            SchemaDescription = editor.SchemaDescription,
            RevisionReason = string.Empty,
            Tags = TagTextValueNormalizer.NormalizeTags(editor.Definition.Tags, 12).ToImmutableArray()
        };
    }

    public static bool TryCreateMutation(
        DefinitionEditorValues values,
        out LlmChatDefinitionMutation? mutation,
        out string validationMessage) {
        if (string.IsNullOrWhiteSpace(values.Name)) {
            return Invalid("Enter a definition name.", out mutation, out validationMessage);
        }

        if (values.ProviderProfileId is not { } providerProfileId || providerProfileId == Guid.Empty) {
            return Invalid("Select a provider.", out mutation, out validationMessage);
        }

        if (string.IsNullOrWhiteSpace(values.Model)) {
            return Invalid("Select or enter a model.", out mutation, out validationMessage);
        }

        if (values.TimeoutSeconds is { } timeoutSeconds && (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0)) {
            return Invalid("Timeout must be a positive number of seconds.", out mutation, out validationMessage);
        }

        if (!IsValidJson(values.ModelParameterConfigurationJson)) {
            return Invalid("Model parameter configuration must be valid JSON.", out mutation, out validationMessage);
        }

        if (values.ResponseFormat == DefinitionEditorFormat.JsonSchema &&
            (string.IsNullOrWhiteSpace(values.SchemaName) || !IsValidJson(values.SchemaJson))) {
            return Invalid("JSON schema output requires a schema name and valid JSON schema.", out mutation, out validationMessage);
        }

        var tags = TagTextValueNormalizer.NormalizeTags(values.Tags, 12);
        mutation = new(
            values.Name,
            values.Summary,
            values.AvatarImageUrl,
            values.SystemPrompt,
            providerProfileId,
            values.Model,
            values.Temperature,
            ToContract(values.ThinkingEffort),
            values.ModelParameterConfigurationJson,
            values.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            ToContract(values.ResponseFormat),
            values.SchemaJson,
            values.SchemaName,
            values.SchemaDescription,
            values.RevisionReason,
            tags.ToImmutableArray());
        validationMessage = string.Empty;
        return true;
    }

    private static bool IsValidJson(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return true;
        }

        try {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException) {
            return false;
        }
    }

    private static bool Invalid(
        string message,
        out LlmChatDefinitionMutation? mutation,
        out string validationMessage) {
        mutation = null;
        validationMessage = message;
        return false;
    }

    public static DefinitionEditorEffort? ToPresentation(LlmChatThinkingEffort? value) => value switch {
        null => null,
        LlmChatThinkingEffort.None => DefinitionEditorEffort.None,
        LlmChatThinkingEffort.Minimal => DefinitionEditorEffort.Minimal,
        LlmChatThinkingEffort.Low => DefinitionEditorEffort.Low,
        LlmChatThinkingEffort.Medium => DefinitionEditorEffort.Medium,
        LlmChatThinkingEffort.High => DefinitionEditorEffort.High,
        LlmChatThinkingEffort.ExtraHigh => DefinitionEditorEffort.ExtraHigh,
        LlmChatThinkingEffort.Max => DefinitionEditorEffort.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static LlmChatThinkingEffort? ToContract(DefinitionEditorEffort? value) => value switch {
        null => null,
        DefinitionEditorEffort.None => LlmChatThinkingEffort.None,
        DefinitionEditorEffort.Minimal => LlmChatThinkingEffort.Minimal,
        DefinitionEditorEffort.Low => LlmChatThinkingEffort.Low,
        DefinitionEditorEffort.Medium => LlmChatThinkingEffort.Medium,
        DefinitionEditorEffort.High => LlmChatThinkingEffort.High,
        DefinitionEditorEffort.ExtraHigh => LlmChatThinkingEffort.ExtraHigh,
        DefinitionEditorEffort.Max => LlmChatThinkingEffort.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static DefinitionEditorFormat ToPresentation(LlmChatUiResponseFormatKind value) => value switch {
        LlmChatUiResponseFormatKind.Text => DefinitionEditorFormat.Text,
        LlmChatUiResponseFormatKind.Json => DefinitionEditorFormat.Json,
        LlmChatUiResponseFormatKind.JsonSchema => DefinitionEditorFormat.JsonSchema,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static LlmChatUiResponseFormatKind ToContract(DefinitionEditorFormat value) => value switch {
        DefinitionEditorFormat.Text => LlmChatUiResponseFormatKind.Text,
        DefinitionEditorFormat.Json => LlmChatUiResponseFormatKind.Json,
        DefinitionEditorFormat.JsonSchema => LlmChatUiResponseFormatKind.JsonSchema,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
