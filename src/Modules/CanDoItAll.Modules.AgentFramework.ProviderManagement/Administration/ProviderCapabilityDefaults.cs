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
            OpenAiProviderAdapter.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ScenarioHarnessProviderAdapter.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ProcessMockProviderAdapter.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            ComfyUiProviderAdapter.PluginKey => new(
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                SupportsVision: false),
            OllamaProviderAdapter.PluginKey or OllamaRemoteProviderAdapter.PluginKey => new(
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                SupportsVision: false),
            _ => Disabled
        };
    }
}
