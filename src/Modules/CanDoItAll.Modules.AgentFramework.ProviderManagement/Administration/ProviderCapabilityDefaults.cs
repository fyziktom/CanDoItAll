namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record ProviderCapabilityDefaults(
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    bool SupportsVision)
{
    public static ProviderCapabilityDefaults Disabled { get; } = new(
        SupportsStreaming: false,
        SupportsToolCalling: false,
        SupportsStructuredOutput: false,
        SupportsVision: false);

    public static ProviderCapabilityDefaults Resolve(string? pluginKey)
    {
        if (string.IsNullOrWhiteSpace(pluginKey))
        {
            return Disabled;
        }

        return pluginKey.Trim() switch
        {
            OpenAiProviderAdministrationConnector.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ScenarioHarnessProviderAdministrationConnector.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ProcessMockProviderAdministrationConnector.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ComfyUiProviderAdministrationConnector.PluginKey => new(
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                SupportsVision: false),
            OllamaProviderAdministrationConnector.PluginKey or OllamaRemoteProviderAdministrationConnector.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            _ => Disabled
        };
    }
}
