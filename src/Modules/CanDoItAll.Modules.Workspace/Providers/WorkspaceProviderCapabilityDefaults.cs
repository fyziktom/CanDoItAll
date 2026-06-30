namespace CanDoItAll.Modules.Workspace;

internal sealed record WorkspaceProviderCapabilityDefaults(
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    bool SupportsVision)
{
    public static WorkspaceProviderCapabilityDefaults Disabled { get; } = new(
        SupportsStreaming: false,
        SupportsToolCalling: false,
        SupportsStructuredOutput: false,
        SupportsVision: false);

    public static WorkspaceProviderCapabilityDefaults Resolve(string? pluginKey)
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
                SupportsStructuredOutput: false,
                SupportsVision: false),
            _ => Disabled
        };
    }
}
