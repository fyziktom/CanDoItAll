using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

using ProviderProfileService = CanDoItAll.AgentFramework.Core.ProviderProfileService;

public sealed class ProviderProfileThinkingCapabilityTests
{
    [Fact]
    public void ApplyHealthResult_PersistsNormalizedPerModelThinkingCapabilities()
    {
        var service = new ProviderProfileService();
        var checkedAtUtc = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

        var updated = service.ApplyHealthResult(CreateProvider(), CreateHealthResult(), checkedAtUtc);

        Assert.Equal(checkedAtUtc, updated.LastCheckedAtUtc);
        Assert.Collection(
            updated.ModelThinkingEffortCapabilities,
            capability =>
            {
                Assert.Equal("llama3.2:3b", capability.Model);
                Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, capability.Status);
                Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, capability.Source);
                Assert.Empty(capability.AllowedEfforts);
                Assert.Equal("llama", capability.ModelFamily);
            },
            capability =>
            {
                Assert.Equal("qwen3.5:2b", capability.Model);
                Assert.Equal(AgentThinkingEffortSupportStatus.Supported, capability.Status);
                Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, capability.Source);
                Assert.Equal(
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High],
                    capability.AllowedEfforts);
                Assert.Equal("qwen", capability.ModelFamily);
                Assert.Equal("Configurable thinking", capability.Summary);
            });
    }

    [Fact]
    public void ApplyHealthResult_PreservesAzureCapabilitiesWhenRefreshDoesNotProvideMetadata()
    {
        var service = new ProviderProfileService();
        var provider = CreateAzureProvider();
        var checkedAtUtc = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var healthResult = new ProviderHealthResult(
            Success: true,
            Summary: "Healthy",
            SuggestedModels: ["reasoning-deployment", "general-deployment"]);

        var updated = service.ApplyHealthResult(provider, healthResult, checkedAtUtc);

        Assert.Equal(checkedAtUtc, updated.LastCheckedAtUtc);
        Assert.Equal(["reasoning-deployment", "general-deployment"], updated.SuggestedModels);
        AssertEquivalentCapability(
            Assert.Single(provider.ModelThinkingEffortCapabilities),
            Assert.Single(updated.ModelThinkingEffortCapabilities));
    }

    [Fact]
    public void ApplyHealthResult_ClearsAzureCapabilitiesWhenRefreshProvidesAuthoritativeEmptyMetadata()
    {
        var service = new ProviderProfileService();
        var healthResult = new ProviderHealthResult(
            Success: true,
            Summary: "Healthy",
            SuggestedModels: ["general-deployment"])
        {
            ModelThinkingEffortCapabilities = []
        };

        var updated = service.ApplyHealthResult(
            CreateAzureProvider(),
            healthResult,
            DateTimeOffset.UtcNow);

        Assert.Empty(updated.ModelThinkingEffortCapabilities);
    }

    [Fact]
    public void CreateProfile_PreservesDiscoveredThinkingCapabilitiesDuringNonIdentityEdit()
    {
        var service = new ProviderProfileService();
        var checkedAtUtc = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var current = service.ApplyHealthResult(CreateProvider(), CreateHealthResult(), checkedAtUtc);
        var editor = service.CreateEditor(current);
        editor.Name = " Edited local provider ";
        editor.BaseUrl = "http://localhost:11434/";
        editor.DefaultModel = " llama3.2:3b ";

        var updated = service.CreateProfile(editor, current);

        Assert.Equal(current.Id, updated.Id);
        Assert.Equal("Edited local provider", updated.Name);
        Assert.Equal("http://localhost:11434", updated.BaseUrl);
        Assert.Equal("llama3.2:3b", updated.DefaultModel);
        Assert.Equal(current.HealthStatus, updated.HealthStatus);
        Assert.Equal(checkedAtUtc, updated.LastCheckedAtUtc);
        Assert.Equal(current.ModelThinkingEffortCapabilities.Count, updated.ModelThinkingEffortCapabilities.Count);

        for (var index = 0; index < current.ModelThinkingEffortCapabilities.Count; index++)
        {
            AssertEquivalentCapability(
                current.ModelThinkingEffortCapabilities[index],
                updated.ModelThinkingEffortCapabilities[index]);
        }
    }

    [Fact]
    public void EditorRoundTrip_PreservesThinkingCapabilitiesWithoutCurrentProfileFallback()
    {
        var service = new ProviderProfileService();
        var current = service.ApplyHealthResult(
            CreateProvider(),
            CreateHealthResult(),
            DateTimeOffset.UtcNow);

        var updated = service.CreateProfile(service.CreateEditor(current));

        Assert.Equal(
            current.ModelThinkingEffortCapabilities.Count,
            updated.ModelThinkingEffortCapabilities.Count);
        for (var index = 0; index < current.ModelThinkingEffortCapabilities.Count; index++)
        {
            AssertEquivalentCapability(
                current.ModelThinkingEffortCapabilities[index],
                updated.ModelThinkingEffortCapabilities[index]);
        }
    }

    [Fact]
    public void CreateProfile_PreservesExplicitAzureKindForOrdinaryAzureName()
    {
        var service = new ProviderProfileService();
        var editor = service.CreateEditor();
        editor.Name = "Azure OpenAI";
        editor.Kind = ProviderKind.AzureOpenAi;
        editor.BaseUrl = "https://example.openai.azure.com";
        editor.DefaultModel = "reasoning-deployment";
        editor.Transport = ProviderTransportKind.ChatCompletions;

        var provider = service.CreateProfile(editor);

        Assert.Equal(ProviderKind.AzureOpenAi, provider.Kind);
    }

    [Fact]
    public void CreateProfile_InvalidatesDiscoveredThinkingCapabilitiesWhenBaseUrlChanges()
    {
        var service = new ProviderProfileService();
        var current = service.ApplyHealthResult(
            CreateProvider(),
            CreateHealthResult(),
            DateTimeOffset.UtcNow);
        var editor = service.CreateEditor(current);
        editor.BaseUrl = "http://localhost:11435";

        var updated = service.CreateProfile(editor, current);

        Assert.Equal("http://localhost:11435", updated.BaseUrl);
        Assert.Empty(updated.ModelThinkingEffortCapabilities);
    }

    [Fact]
    public void CreateProfile_InvalidatesOllamaDiscoveryWhenProviderKindChangesToOpenAi()
    {
        var service = new ProviderProfileService();
        var current = service.ApplyHealthResult(
            CreateProvider(),
            CreateHealthResult(),
            DateTimeOffset.UtcNow);
        var editor = service.CreateEditor(current);
        editor.Kind = ProviderKind.OpenAi;
        editor.BaseUrl = "https://api.openai.test/v1";
        editor.DefaultModel = "gpt-4.1";

        var updated = service.CreateProfile(editor, current);

        Assert.Equal(ProviderKind.OpenAi, updated.Kind);
        Assert.Empty(updated.ModelThinkingEffortCapabilities);
    }

    [Fact]
    public void ResolveCapability_IgnoresStoredOllamaDiscoveryForOpenAiProvider()
    {
        const string model = "gpt-4.1";
        var provider = CreateProvider() with
        {
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.test/v1",
            DefaultModel = model,
            ModelThinkingEffortCapabilities =
            [
                AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                    model,
                    "ollama-family",
                    AgentThinkingEffortSupportStatus.Supported)
            ]
        };

        var capability = AgentThinkingEffortPolicy.ResolveCapability(provider, model);

        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, capability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Defined, capability.Source);
        Assert.Empty(capability.AllowedEfforts);
    }

    [Fact]
    public void ApplyHealthResult_RejectsDuplicateModelThinkingCapabilitiesCaseInsensitively()
    {
        var service = new ProviderProfileService();
        var healthResult = CreateHealthResult();
        var capabilities = Assert.IsAssignableFrom<IReadOnlyList<ProviderModelThinkingEffortCapability>>(
            healthResult.ModelThinkingEffortCapabilities);
        var supportedCapability = Assert.Single(
            capabilities,
            capability => capability.Status == AgentThinkingEffortSupportStatus.Supported);
        healthResult = healthResult with
        {
            ModelThinkingEffortCapabilities =
            [
                .. capabilities,
                supportedCapability with { Model = " QWEN3.5:2B " }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.ApplyHealthResult(CreateProvider(), healthResult, DateTimeOffset.UtcNow));

        Assert.Contains("duplicate model", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qwen3.5:2b", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderHealthResult CreateHealthResult()
    {
        return new ProviderHealthResult(
            Success: true,
            Summary: "Healthy",
            SuggestedModels: [" qwen3.5:2b ", "QWEN3.5:2B", " llama3.2:3b "])
        {
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    "qwen3.5:2b",
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [AgentReasoningEffortLevel.High, AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High],
                    "qwen",
                    "Configurable thinking"),
                new ProviderModelThinkingEffortCapability(
                    " llama3.2:3b ",
                    AgentThinkingEffortSupportStatus.Unsupported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [],
                    " llama ",
                    " Thinking is unavailable ")
            ]
        };
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Local Ollama",
            Kind: ProviderKind.Ollama,
            BaseUrl: "http://localhost:11434",
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: "qwen3.5:2b",
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static ProviderProfile CreateAzureProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Azure OpenAI",
            Kind: ProviderKind.AzureOpenAi,
            BaseUrl: "https://azure-openai.test",
            ApiKeyEnvironmentVariable: "AZURE_OPENAI_API_KEY",
            DefaultModel: "reasoning-deployment",
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["reasoning-deployment"])
        {
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    "reasoning-deployment",
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Defined,
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.Medium, AgentReasoningEffortLevel.High],
                    Summary: "Provider-scoped Azure deployment metadata.",
                    ControlMode: AgentThinkingEffortControlMode.EffortLevels)
            ]
        };
    }

    private static void AssertEquivalentCapability(
        ProviderModelThinkingEffortCapability expected,
        ProviderModelThinkingEffortCapability actual)
    {
        Assert.Equal(expected.Model, actual.Model);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Source, actual.Source);
        Assert.Equal(expected.AllowedEfforts, actual.AllowedEfforts);
        Assert.Equal(expected.ModelFamily, actual.ModelFamily);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(expected.ControlMode, actual.ControlMode);
    }
}
