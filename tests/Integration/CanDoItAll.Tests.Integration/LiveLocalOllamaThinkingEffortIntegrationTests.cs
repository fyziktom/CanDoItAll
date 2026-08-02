using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Integration;

public sealed class LiveLocalOllamaThinkingEffortIntegrationTests
{
    internal const string EnabledEnvironmentVariable = "CANDOITALL_RUN_LIVE_OLLAMA_VALIDATION";
    internal const string BaseUrlEnvironmentVariable = "CANDOITALL_LIVE_OLLAMA_BASE_URL";

    private const string DefaultBaseUrl = "http://127.0.0.1:11434";
    private static readonly IReadOnlyList<string> PreferredBinaryThinkingModels =
    [
        "qwen3.5:2b",
        "gemma4:12b"
    ];
    private static readonly IReadOnlyList<string> PreferredGptOssModels =
    [
        "gptoss32k:latest",
        "gpt-oss:20b"
    ];
    private static readonly IReadOnlyList<string> PreferredDeepSeekModels =
    [
        "deepseek-r1:14b",
        "deepseek-r1:8b",
        "deepseek-r1:32b"
    ];
    private static readonly IReadOnlyList<string> PreferredNonThinkingModels =
    [
        "llama3.2:3b",
        "gemma3:1b",
        "qwen2.5:3b"
    ];

    [LiveLocalOllamaFact]
    [Trait("Category", "LiveProcess")]
    public async Task Installed_catalog_and_native_effort_mapping_match_thinking_capabilities()
    {
        var baseUrl = ResolveBaseUrl();
        using var handler = new RequestCountingHandler(new HttpClientHandler());
        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        var driver = new OllamaProviderDriver(httpClient);
        var catalogProvider = CreateProvider(baseUrl);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var models = await driver.ListModelsAsync(
            new ProviderModelCatalogRequest(
                catalogProvider,
                AgentProviderCapabilityKind.ChatCompletion),
            timeout.Token);
        var binaryThinkingModels = SelectBinaryThinkingModels(models);
        Assert.True(
            binaryThinkingModels.Count == 2,
            $"Live Ollama validation requires installed Qwen and Gemma binary-thinking model families. " +
            $"Discovered models: {DescribeCapabilities(models)}");
        var gptOssModel = SelectGptOssModel(models);
        Assert.True(
            gptOssModel is not null,
            $"Live Ollama validation requires an installed GPT-OSS model. " +
            $"Discovered models: {DescribeCapabilities(models)}");
        var selectedGptOssModel = Assert.IsType<ProviderModelDescriptor>(gptOssModel);
        var nonThinkingModel = SelectNonThinkingModel(models);
        Assert.True(
            nonThinkingModel is not null,
            $"Live Ollama validation requires one installed non-thinking model. " +
            $"Discovered models: {DescribeCapabilities(models)}");
        var selectedNonThinkingModel = Assert.IsType<ProviderModelDescriptor>(nonThinkingModel);
        var deepSeekModel = SelectDeepSeekModel(models);
        if (deepSeekModel is not null)
        {
            var deepSeekDiscovery = Assert.IsType<ProviderModelThinkingEffortCapability>(
                deepSeekModel.ThinkingEffortCapability);
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, deepSeekDiscovery.Status);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, deepSeekDiscovery.Source);
            Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, deepSeekDiscovery.ControlMode);
        }

        foreach (var model in binaryThinkingModels)
        {
            var discoveredCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
                model.ThinkingEffortCapability);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, discoveredCapability.Source);
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, discoveredCapability.Status);
            Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, discoveredCapability.ControlMode);
            Assert.Equal(
                [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium],
                discoveredCapability.AllowedEfforts);

            var definedCapability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
                ProviderKind.Ollama,
                ProviderTransportKind.ChatCompletions,
                model.Model);
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, definedCapability.Status);
            Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, definedCapability.ControlMode);
            Assert.Equal(
                [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium],
                definedCapability.AllowedEfforts);
        }

        var gptOssCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            selectedGptOssModel.ThinkingEffortCapability);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, gptOssCapability.Status);
        Assert.Equal(AgentThinkingEffortControlMode.EffortLevels, gptOssCapability.ControlMode);
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            gptOssCapability.AllowedEfforts);
        var definedGptOssCapability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            selectedGptOssModel.Model);
        Assert.Equal(gptOssCapability.AllowedEfforts, definedGptOssCapability.AllowedEfforts);
        Assert.Equal(gptOssCapability.ControlMode, definedGptOssCapability.ControlMode);

        var discoveredCapabilities = models
            .Select(model => model.ThinkingEffortCapability)
            .Where(capability => capability is not null)
            .Cast<ProviderModelThinkingEffortCapability>()
            .ToList();
        var provider = catalogProvider with
        {
            DefaultModel = binaryThinkingModels[0].Model,
            SuggestedModels = models.Select(model => model.Model).ToList(),
            ModelThinkingEffortCapabilities = discoveredCapabilities
        };
        if (deepSeekModel is not null)
        {
            var resolvedDeepSeekCapability = AgentThinkingEffortPolicy.ResolveCapability(
                provider,
                deepSeekModel.Model);
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, resolvedDeepSeekCapability.Status);
            Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, resolvedDeepSeekCapability.ControlMode);
        }

        var enabledConfiguration = CreateRequestConfiguration(AgentReasoningEffortLevel.Medium);

        foreach (var model in binaryThinkingModels)
        {
            var enabledResult = await driver.CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    model.Model,
                    "Return only a short final answer. Do not include reasoning.",
                    [],
                    "Reply with OK.",
                    ModelParameterConfigurationJson: enabledConfiguration),
                timeout.Token);

            Assert.Equal(model.Model, enabledResult.Model);
            Assert.True(
                enabledResult.InputTokens > 0,
                $"Ollama model '{model.Model}' reported no input tokens with thinking enabled.");
            Assert.True(
                enabledResult.OutputTokens > 0,
                $"Ollama model '{model.Model}' reported no output tokens with thinking enabled.");

            var disabledResult = await driver.CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    model.Model,
                    "Return only a short final answer. Do not include reasoning.",
                    [],
                    "Reply with OK.",
                    ModelParameterConfigurationJson:
                        CreateRequestConfiguration(AgentReasoningEffortLevel.None)),
                timeout.Token);

            Assert.True(
                !string.IsNullOrWhiteSpace(disabledResult.ResponseText),
                $"Ollama model '{model.Model}' returned no visible answer with thinking explicitly disabled.");
            Assert.True(
                disabledResult.OutputTokens > 0,
                $"Ollama model '{model.Model}' reported no output tokens with thinking explicitly disabled.");
        }

        var requestCountBeforeGptOssDisable = handler.RequestCount;
        var gptOssDisableException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    selectedGptOssModel.Model,
                    "Return only a short final answer.",
                    [],
                    "Reply with OK.",
                    ModelParameterConfigurationJson:
                        CreateRequestConfiguration(AgentReasoningEffortLevel.None)),
                timeout.Token));

        Assert.Contains("Allowed values are low, medium, high", gptOssDisableException.Message, StringComparison.Ordinal);
        Assert.Equal(requestCountBeforeGptOssDisable, handler.RequestCount);

        var gptOssResult = await driver.CompleteChatAsync(
            new ProviderChatCompletionRequest(
                provider,
                selectedGptOssModel.Model,
                "Return only a short final answer.",
                [],
                "Reply with OK.",
                ModelParameterConfigurationJson:
                    CreateRequestConfiguration(AgentReasoningEffortLevel.Low)),
            timeout.Token);

        Assert.Equal(selectedGptOssModel.Model, gptOssResult.Model);
        Assert.True(
            gptOssResult.OutputTokens > 0,
            $"Ollama model '{selectedGptOssModel.Model}' reported no output tokens with Low thinking effort.");

        var negativeCapability = Assert.IsType<ProviderModelThinkingEffortCapability>(
            selectedNonThinkingModel.ThinkingEffortCapability);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, negativeCapability.Source);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, negativeCapability.Status);
        var requestCountBeforeNegative = handler.RequestCount;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    selectedNonThinkingModel.Model,
                    "Return only a short final answer.",
                    [],
                    "Reply with OK.",
                    ModelParameterConfigurationJson:
                        CreateRequestConfiguration(AgentReasoningEffortLevel.Low)),
                timeout.Token));

        Assert.Contains("cannot be applied", exception.Message, StringComparison.Ordinal);
        Assert.Equal(requestCountBeforeNegative, handler.RequestCount);
    }

    private static ProviderProfile CreateProvider(string baseUrl)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Live local Ollama",
            ProviderKind.Ollama,
            baseUrl,
            string.Empty,
            string.Empty,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static IReadOnlyList<ProviderModelDescriptor> SelectBinaryThinkingModels(
        IReadOnlyList<ProviderModelDescriptor> models)
    {
        var selected = new List<ProviderModelDescriptor>();
        var selectedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = models
            .Where(model =>
                model.ThinkingEffortCapability?.Status == AgentThinkingEffortSupportStatus.Supported)
            .Where(model =>
                model.ThinkingEffortCapability?.ControlMode == AgentThinkingEffortControlMode.BooleanToggle)
            .Where(model =>
                AgentThinkingEffortPolicy.ResolveDefinedCapability(
                    ProviderKind.Ollama,
                    ProviderTransportKind.ChatCompletions,
                    model.Model).Status == AgentThinkingEffortSupportStatus.Supported)
            .OrderBy(model => ResolvePriority(model.Model, PreferredBinaryThinkingModels))
            .ThenBy(model => model.Model, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var family = candidate.ThinkingEffortCapability?.ModelFamily.Trim();
            if (string.IsNullOrWhiteSpace(family) || !selectedFamilies.Add(family))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == 2)
            {
                break;
            }
        }

        return selected;
    }

    private static ProviderModelDescriptor? SelectGptOssModel(
        IReadOnlyList<ProviderModelDescriptor> models)
    {
        return models
            .Where(model =>
                model.ThinkingEffortCapability?.Status == AgentThinkingEffortSupportStatus.Supported)
            .Where(model =>
                model.ThinkingEffortCapability?.ControlMode == AgentThinkingEffortControlMode.EffortLevels)
            .Where(model => string.Equals(
                model.ThinkingEffortCapability?.ModelFamily,
                "gptoss",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(model => ResolvePriority(model.Model, PreferredGptOssModels))
            .ThenBy(model => model.Model, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static ProviderModelDescriptor? SelectDeepSeekModel(
        IReadOnlyList<ProviderModelDescriptor> models)
    {
        return models
            .Where(model => model.Model.StartsWith("deepseek-r1", StringComparison.OrdinalIgnoreCase))
            .OrderBy(model => ResolvePriority(model.Model, PreferredDeepSeekModels))
            .ThenBy(model => model.Model, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static ProviderModelDescriptor? SelectNonThinkingModel(
        IReadOnlyList<ProviderModelDescriptor> models)
    {
        return models
            .Where(model =>
                model.ThinkingEffortCapability?.Status == AgentThinkingEffortSupportStatus.Unsupported)
            .OrderBy(model => ResolvePriority(model.Model, PreferredNonThinkingModels))
            .ThenBy(model => model.Model, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int ResolvePriority(string model, IReadOnlyList<string> preferredModels)
    {
        for (var index = 0; index < preferredModels.Count; index++)
        {
            if (string.Equals(model, preferredModels[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return preferredModels.Count;
    }

    private static string CreateRequestConfiguration(AgentReasoningEffortLevel effort)
    {
        var configuration = new JsonObject
        {
            [AgentThinkingEffortPolicy.ModelParametersConfigurationPropertyName] = new JsonObject
            {
                [AgentProviderModelParameterPolicy.MaxOutputTokensConfigurationPropertyName] = 16
            }
        };
        return AgentThinkingEffortPolicy.WriteAgentOverride(configuration.ToJsonString(), effort);
    }

    private static string ResolveBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? DefaultBaseUrl
            : configured.Trim();
        Assert.True(
            Uri.TryCreate(baseUrl, UriKind.Absolute, out _),
            $"Environment variable '{BaseUrlEnvironmentVariable}' must contain an absolute URL.");
        return baseUrl.TrimEnd('/');
    }

    private static string DescribeCapabilities(IReadOnlyList<ProviderModelDescriptor> models)
    {
        return string.Join(
            ", ",
            models.Select(model =>
                $"{model.Model}=" +
                $"{model.ThinkingEffortCapability?.Status.ToString() ?? "Missing"}" +
                $"/{model.ThinkingEffortCapability?.ModelFamily ?? "unknown-family"}"));
    }

    private sealed class RequestCountingHandler(HttpMessageHandler innerHandler) :
        DelegatingHandler(innerHandler)
    {
        private int requestCount;

        public int RequestCount => Volatile.Read(ref requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

internal sealed class LiveLocalOllamaFactAttribute : FactAttribute
{
    public LiveLocalOllamaFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    LiveLocalOllamaThinkingEffortIntegrationTests.EnabledEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip =
                $"Set {LiveLocalOllamaThinkingEffortIntegrationTests.EnabledEnvironmentVariable}=true " +
                "to run the installed-model Ollama validation. The test never downloads models.";
        }
    }
}
