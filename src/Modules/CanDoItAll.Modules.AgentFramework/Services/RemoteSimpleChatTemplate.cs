using System.ComponentModel;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

[Description("The explicitly shared current revision of a Simple Chat definition.")]
public sealed record RemoteSimpleChatTemplate(
    [property: Description("Publisher identity, definition identity, revision, display text and avatar.")] RemoteTemplateRow Row,
    [property: Description("System instructions used when a new conversation is started.")] string SystemPrompt,
    [property: Description("Labels from the published definition.")] IReadOnlyList<string> Tags,
    [property: Description("Model settings authored on the Simple Chat definition.")] RemoteSimpleChatSettings Settings,
    [property: Description("Optional execution time limit of the definition.")] TimeSpan? Timeout,
    [property: Description("Requested local response format and optional output schema.")] RemoteSimpleChatResponseFormat? ResponseFormat,
    [property: Description("Required model features and the preferred published routing model.")] RemoteTemplateModelRequirement Model);

[Description("Portable parameters for the selected chat model.")]
public sealed record RemoteSimpleChatSettings(
    [property: Description("Optional model temperature, from zero to two.")] double? Temperature,
    [property: Description("Optional JSON object containing the definition's output token limits.")] string ModelParameterConfigurationJson,
    [property: Description("Requested reasoning effort, 0 None, 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max or 6 Minimal.")] AgentReasoningEffortLevel? ThinkingEffort) {
    public static RemoteSimpleChatSettings From(LlmModelSettings settings) {
        RemoteTemplateModelParameters.RequirePortable(settings.ModelParameterConfigurationJson);
        return new(settings.Temperature, settings.ModelParameterConfigurationJson, settings.ThinkingEffort);
    }

    public LlmModelSettings ToLocal() {
        RemoteTemplateModelParameters.RequirePortable(ModelParameterConfigurationJson);
        return new(Temperature, ModelParameterConfigurationJson) { ThinkingEffort = ThinkingEffort };
    }
}

[Description("An optional response format for conversations created from this definition.")]
public sealed record RemoteSimpleChatResponseFormat(
    [property: Description("Whether the model must respond with JSON.")] bool RequireJson,
    [property: Description("Optional JSON schema for structured responses.")] string SchemaJson,
    [property: Description("Name of the response schema.")] string SchemaName,
    [property: Description("Explanation of the requested response schema.")] string SchemaDescription) {
    public LlmResponseFormat ToLocal() => new(RequireJson, SchemaJson, SchemaName, SchemaDescription);
}
