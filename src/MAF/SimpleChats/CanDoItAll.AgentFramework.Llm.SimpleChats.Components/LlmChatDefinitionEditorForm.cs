using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatDefinitionEditorForm
{
    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string AvatarImageUrl { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public Guid? ProviderProfileId { get; set; }

    public string Model { get; set; } = string.Empty;

    public double? Temperature { get; set; }

    public LlmChatThinkingEffort? ThinkingEffort { get; set; }

    public string ModelParameterConfigurationJson { get; set; } = string.Empty;

    public double? TimeoutSeconds { get; set; }

    public LlmChatUiResponseFormatKind ResponseFormat { get; set; }

    public string SchemaJson { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string SchemaDescription { get; set; } = string.Empty;

    public string RevisionReason { get; set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public static LlmChatDefinitionEditorForm From(LlmChatDefinitionEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return new()
        {
            Name = editor.Definition.Name,
            Summary = editor.Definition.Summary,
            AvatarImageUrl = editor.Definition.AvatarImageUrl,
            SystemPrompt = editor.SystemPrompt,
            ProviderProfileId = editor.ProviderProfileId,
            Model = editor.Model,
            Temperature = editor.Temperature,
            ThinkingEffort = editor.ThinkingEffort,
            ModelParameterConfigurationJson = editor.ModelParameterConfigurationJson,
            TimeoutSeconds = editor.Timeout?.TotalSeconds,
            ResponseFormat = editor.ResponseFormat,
            SchemaJson = editor.SchemaJson,
            SchemaName = editor.SchemaName,
            SchemaDescription = editor.SchemaDescription,
            RevisionReason = string.Empty,
            Tags = TagTextValueNormalizer.NormalizeTags(editor.Definition.Tags, 12)
        };
    }

    public bool TryCreateMutation(
        out LlmChatDefinitionMutation? mutation,
        out string validationMessage)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Invalid("Enter a definition name.", out mutation, out validationMessage);
        }

        if (ProviderProfileId is not { } providerProfileId || providerProfileId == Guid.Empty)
        {
            return Invalid("Select a provider.", out mutation, out validationMessage);
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            return Invalid("Select or enter a model.", out mutation, out validationMessage);
        }

        if (TimeoutSeconds is { } timeoutSeconds && (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0))
        {
            return Invalid("Timeout must be a positive number of seconds.", out mutation, out validationMessage);
        }

        if (!IsValidJson(ModelParameterConfigurationJson))
        {
            return Invalid("Model parameter configuration must be valid JSON.", out mutation, out validationMessage);
        }

        if (ResponseFormat == LlmChatUiResponseFormatKind.JsonSchema &&
            (string.IsNullOrWhiteSpace(SchemaName) || !IsValidJson(SchemaJson)))
        {
            return Invalid("JSON schema output requires a schema name and valid JSON schema.", out mutation, out validationMessage);
        }

        var tags = TagTextValueNormalizer.NormalizeTags(Tags, 12);
        mutation = new(
            Name,
            Summary,
            AvatarImageUrl,
            SystemPrompt,
            providerProfileId,
            Model,
            Temperature,
            ThinkingEffort,
            ModelParameterConfigurationJson,
            TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            ResponseFormat,
            SchemaJson,
            SchemaName,
            SchemaDescription,
            RevisionReason,
            tags);
        validationMessage = string.Empty;
        return true;
    }

    private static bool IsValidJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Invalid(
        string message,
        out LlmChatDefinitionMutation? mutation,
        out string validationMessage)
    {
        mutation = null;
        validationMessage = message;
        return false;
    }
}
