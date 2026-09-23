using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface IProviderManifestCatalog
{
    ConnectorPluginManifest? ResolveManifest(
        string? connectorPluginKey,
        ProviderKind? legacyProviderKind = null);

    IReadOnlyList<ConnectorPluginManifest> ListManifests();
}

public static class ProviderConnectorKeys
{
    public const string OpenAi = "provider.openai";
    public const string ScenarioHarness = "provider.scenario-harness";
    public const string ProcessMock = "provider.process-mock";
    public const string ComfyUi = "provider.comfyui.local";
    public const string Ollama = "provider.ollama.local";
    public const string OllamaRemote = "provider.ollama.remote";
    public const string SharedImport = "provider.candoitall-shared";
}

public static class ProviderConnectorDefaults
{
    public const string OpenAiModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    public const string ScenarioHarnessBaseUrl = "scenario://harness";
    public const string ScenarioHarnessModel = "scenario-local";
    public const string ProcessMockBaseUrl = "process-mock://agents";
    public const string ProcessMockModel = "process-mock-local";
    public const string ComfyUiModel = "comfyui-workflow";
    public const string OllamaModel = "llama3.1";
}
