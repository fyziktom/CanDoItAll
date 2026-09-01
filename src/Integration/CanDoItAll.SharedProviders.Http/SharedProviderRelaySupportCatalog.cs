using System.Collections.Frozen;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public sealed class SharedProviderRelaySupportCatalog : ISharedProviderRelaySupportCatalog
{
    private const char KeySeparator = '\u001f';

    internal static readonly IReadOnlyList<SharedProviderRelayAdapterDescriptor> ProductionDescriptors =
        Array.AsReadOnly(new[]
        {
            CreateOpenAiChatDescriptor(),
            CreateOpenAiImageDescriptor(),
            CreateOllamaChatDescriptor(SharedProviderConnectorPluginKeys.OllamaLocal),
            CreateOllamaChatDescriptor(SharedProviderConnectorPluginKeys.OllamaRemote),
            CreateComfyUiImageDescriptor()
        });

    private readonly IReadOnlyList<SharedProviderRelayAdapterDescriptor> descriptors;
    private readonly FrozenDictionary<string, SharedProviderRelayAdapterDescriptor> descriptorByConnectorAndPurpose;

    public SharedProviderRelaySupportCatalog()
        : this(ProductionDescriptors)
    {
    }

    internal SharedProviderRelaySupportCatalog(
        IReadOnlyList<SharedProviderRelayAdapterDescriptor> descriptors)
    {
        this.descriptors = descriptors;
        descriptorByConnectorAndPurpose = descriptors.ToFrozenDictionary(
            descriptor => BuildKey(descriptor.ConnectorPluginKey, descriptor.Purpose),
            descriptor => descriptor,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SharedProviderRelayAdapterDescriptor> List()
        => descriptors;

    public bool TryGet(
        string connectorPluginKey,
        SharedProviderPurpose purpose,
        out SharedProviderRelayAdapterDescriptor descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrWhiteSpace(connectorPluginKey) || !Enum.IsDefined(purpose))
        {
            return false;
        }

        return descriptorByConnectorAndPurpose.TryGetValue(
            BuildKey(connectorPluginKey.Trim(), purpose),
            out descriptor!);
    }

    private static SharedProviderRelayAdapterDescriptor CreateOpenAiChatDescriptor()
        => new(
            SharedProviderConnectorPluginKeys.OpenAi,
            SharedProviderPurpose.Chat,
            SharedProviderRelayAdapterClassification.Production,
            new SharedProviderRelaySupportDescriptor(
                new HashSet<SharedProviderRelayOperation>
                {
                    SharedProviderRelayOperation.ChatCompletions,
                    SharedProviderRelayOperation.Responses
                },
                SharedProviderStreamingMode.ServerSentEvents,
                supportsFunctionTools: true,
                supportsParallelFunctionTools: true,
                supportsStructuredOutput: true,
                supportsVisionInput: true,
                supportsBase64Images: false,
                maximumRequestBytes: 4 * 1024 * 1024,
                maximumOutputTokens: 128 * 1024,
                maximumImageCount: 1));

    private static SharedProviderRelayAdapterDescriptor CreateOpenAiImageDescriptor()
        => new(
            SharedProviderConnectorPluginKeys.OpenAi,
            SharedProviderPurpose.ImageGeneration,
            SharedProviderRelayAdapterClassification.Production,
            CreateImageSupport(maximumImageCount: 4));

    private static SharedProviderRelayAdapterDescriptor CreateOllamaChatDescriptor(string connectorPluginKey)
        => new(
            connectorPluginKey,
            SharedProviderPurpose.Chat,
            SharedProviderRelayAdapterClassification.Production,
            new SharedProviderRelaySupportDescriptor(
                new HashSet<SharedProviderRelayOperation>
                {
                    SharedProviderRelayOperation.ChatCompletions
                },
                SharedProviderStreamingMode.ServerSentEvents,
                supportsFunctionTools: true,
                supportsParallelFunctionTools: false,
                supportsStructuredOutput: true,
                supportsVisionInput: false,
                supportsBase64Images: false,
                maximumRequestBytes: 2 * 1024 * 1024,
                maximumOutputTokens: 32 * 1024,
                maximumImageCount: 1));

    private static SharedProviderRelayAdapterDescriptor CreateComfyUiImageDescriptor()
        => new(
            SharedProviderConnectorPluginKeys.ComfyUiLocal,
            SharedProviderPurpose.ImageGeneration,
            SharedProviderRelayAdapterClassification.Production,
            CreateImageSupport(maximumImageCount: 4));

    private static SharedProviderRelaySupportDescriptor CreateImageSupport(int maximumImageCount)
        => new(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ImageGenerations
            },
            SharedProviderStreamingMode.None,
            supportsFunctionTools: false,
            supportsParallelFunctionTools: false,
            supportsStructuredOutput: false,
            supportsVisionInput: false,
            supportsBase64Images: true,
            maximumRequestBytes: 1024 * 1024,
            maximumOutputTokens: 1,
            maximumImageCount);

    internal static string BuildKey(string connectorPluginKey, SharedProviderPurpose purpose)
        => $"{connectorPluginKey}{KeySeparator}{(int)purpose}";
}

internal static class SharedProviderConnectorPluginKeys
{
    public const string OpenAi = "provider.openai";
    public const string OllamaLocal = "provider.ollama.local";
    public const string OllamaRemote = "provider.ollama.remote";
    public const string ComfyUiLocal = "provider.comfyui.local";
}
