using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentDefinitionFactoryThinkingEffortTests
{
    [Fact]
    public void Create_ExplicitNoneCanonicalizesConfigurationAndRehydratesEditor()
    {
        var provider = CreateProvider(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Sol);
        var editor = CreateEditor(provider);
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.None;
        editor.ConfigurationJson =
            """
            {
              "reasoningEffort": "high",
              "think": true,
              "keepRoot": "value",
              "modelParameters": {
                "reasoningEffort": "low",
                "think": false,
                "maxOutputTokens": 512
              }
            }
            """;

        var definition = CreateDefinition(editor, [provider]);
        var rehydrated = AgentEditorModel.FromDefinition(definition);
        using var document = JsonDocument.Parse(definition.ConfigurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");

        Assert.Equal(AgentReasoningEffortLevel.None, rehydrated.ThinkingEffortOverride);
        Assert.Equal("value", root.GetProperty("keepRoot").GetString());
        Assert.False(root.TryGetProperty("reasoningEffort", out _));
        Assert.False(root.TryGetProperty("think", out _));
        Assert.Equal("none", modelParameters.GetProperty("reasoningEffort").GetString());
        Assert.Equal(512, modelParameters.GetProperty("maxOutputTokens").GetInt32());
        Assert.False(modelParameters.TryGetProperty("think", out _));
    }

    [Fact]
    public void Create_NullOverrideRemovesAliasesWithoutProviderValidation()
    {
        var editor = CreateEditor(provider: null);
        editor.ConfigurationJson =
            """
            {
              "reasoningEffort": "high",
              "think": true,
              "keepRoot": "value",
              "modelParameters": {
                "reasoningEffort": "low",
                "think": false,
                "numPredict": 64
              }
            }
            """;

        var definition = CreateDefinition(editor, []);
        using var document = JsonDocument.Parse(definition.ConfigurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");

        Assert.Null(AgentEditorModel.FromDefinition(definition).ThinkingEffortOverride);
        Assert.Equal("value", root.GetProperty("keepRoot").GetString());
        Assert.False(root.TryGetProperty("reasoningEffort", out _));
        Assert.False(root.TryGetProperty("think", out _));
        Assert.Equal(64, modelParameters.GetProperty("numPredict").GetInt32());
        Assert.False(modelParameters.TryGetProperty("reasoningEffort", out _));
        Assert.False(modelParameters.TryGetProperty("think", out _));
    }

    [Fact]
    public void Create_MigratesLegacyThinkForOllamaAtSaveBoundary()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");
        var legacyDefinition = CreateDefinition(CreateEditor(provider), [provider]) with
        {
            ConfigurationJson =
                """{"keepRoot":"value","modelParameters":{"think":false,"numPredict":64}}"""
        };
        var editor = AgentEditorModel.FromDefinition(legacyDefinition);

        var savedDefinition = CreateDefinition(editor, [provider]);

        Assert.Null(editor.ThinkingEffortOverride);
        Assert.Equal(
            AgentReasoningEffortLevel.None,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(
                savedDefinition.ConfigurationJson,
                "agent"));
        using var document = JsonDocument.Parse(savedDefinition.ConfigurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");
        Assert.Equal("value", root.GetProperty("keepRoot").GetString());
        Assert.Equal("none", modelParameters.GetProperty("reasoningEffort").GetString());
        Assert.Equal(64, modelParameters.GetProperty("numPredict").GetInt32());
        Assert.False(modelParameters.TryGetProperty("think", out _));
    }

    [Fact]
    public void Create_ExplicitProviderDefaultResetRemovesLegacyThinkForOllama()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");
        var legacyDefinition = CreateDefinition(CreateEditor(provider), [provider]) with
        {
            ConfigurationJson =
                """{"keepRoot":"value","modelParameters":{"think":false,"numPredict":64}}"""
        };
        var editor = AgentEditorModel.FromDefinition(
            legacyDefinition,
            ProviderKind.Ollama);

        Assert.Equal(AgentReasoningEffortLevel.None, editor.ThinkingEffortOverride);

        editor.ThinkingEffortOverride = null;
        editor.IsThinkingEffortOverrideEdited = true;
        var savedDefinition = CreateDefinition(editor, [provider]);

        Assert.Null(AgentThinkingEffortPolicy.ReadConfiguredEffort(
            savedDefinition.ConfigurationJson,
            "agent"));
        using var document = JsonDocument.Parse(savedDefinition.ConfigurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");
        Assert.Equal("value", root.GetProperty("keepRoot").GetString());
        Assert.Equal(64, modelParameters.GetProperty("numPredict").GetInt32());
        Assert.False(root.TryGetProperty("think", out _));
        Assert.False(modelParameters.TryGetProperty("think", out _));
        Assert.False(modelParameters.TryGetProperty("reasoningEffort", out _));
    }

    [Fact]
    public void Create_DropsLegacyThinkForOpenAiAtSaveBoundary()
    {
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5.4-mini");
        var legacyDefinition = CreateDefinition(CreateEditor(provider), [provider]) with
        {
            ConfigurationJson =
                """{"think":true,"keepRoot":"value","modelParameters":{"think":false,"maxOutputTokens":512}}"""
        };
        var editor = AgentEditorModel.FromDefinition(legacyDefinition);

        var savedDefinition = CreateDefinition(editor, [provider]);

        Assert.Null(editor.ThinkingEffortOverride);
        Assert.Null(AgentThinkingEffortPolicy.ReadConfiguredEffort(
            savedDefinition.ConfigurationJson,
            "agent"));
        using var document = JsonDocument.Parse(savedDefinition.ConfigurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");
        Assert.Equal("value", root.GetProperty("keepRoot").GetString());
        Assert.Equal(512, modelParameters.GetProperty("maxOutputTokens").GetInt32());
        Assert.False(root.TryGetProperty("think", out _));
        Assert.False(modelParameters.TryGetProperty("think", out _));
        Assert.False(modelParameters.TryGetProperty("reasoningEffort", out _));
    }

    [Fact]
    public void Create_InheritedProviderDefaultRejectsDisallowedEffortForSupportedModel()
    {
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5") with
        {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.None)
        };
        var editor = CreateEditor(provider);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(editor, [provider]));

        Assert.Contains("provider thinking-effort override 'none'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gpt-5", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("custom-deployment-west")]
    public void Create_InheritedProviderDefaultIsIgnoredForUnsupportedOrUnknownModel(string model)
    {
        var provider = CreateProvider(ProviderKind.OpenAi, model) with
        {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.High)
        };
        var editor = CreateEditor(provider);

        var definition = CreateDefinition(editor, [provider]);

        Assert.Null(AgentThinkingEffortPolicy.ReadConfiguredEffort(
            definition.ConfigurationJson,
            "agent"));
    }

    [Fact]
    public void Create_ExplicitOverrideRequiresSelectedProvider()
    {
        var editor = CreateEditor(provider: null);
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.Medium;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(editor, []));

        Assert.Contains("requires a selected provider profile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ExplicitOverrideRejectsMissingProviderProfile()
    {
        var editor = CreateEditor(provider: null);
        editor.ProviderProfileId = Guid.NewGuid();
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.Medium;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(editor, []));

        Assert.Contains("missing provider profile", exception.Message, StringComparison.Ordinal);
        Assert.Contains(editor.ProviderProfileId.Value.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gpt-4.1", "does not support configurable thinking effort")]
    [InlineData("custom-deployment-west", "capability is not defined")]
    public void Create_ExplicitOverrideRejectsUnsupportedAndUnknownOpenAiModels(
        string model,
        string expectedMessage)
    {
        var provider = CreateProvider(ProviderKind.OpenAi, model);
        var editor = CreateEditor(provider);
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.Medium;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(editor, [provider]));

        Assert.Contains(model, exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ExplicitOverrideRejectsDisallowedOllamaLevel()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");
        var editor = CreateEditor(provider);
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.ExtraHigh;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(editor, [provider]));

        Assert.Contains("xhigh", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Allowed values", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UsesNormalizedAgentModelBeforeProviderDefault()
    {
        const string agentModel = "gpt-5.4";
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4.1");
        var editor = CreateEditor(provider);
        editor.Model = $" {agentModel} ";
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.High;

        var definition = CreateDefinition(editor, [provider]);

        Assert.Equal(agentModel, definition.Model);
        Assert.Equal(
            AgentReasoningEffortLevel.High,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(definition.ConfigurationJson, "agent"));
    }

    [Fact]
    public void Create_UsesTheExactModelMatrixForOriginalGpt5()
    {
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5");
        var supportedEditor = CreateEditor(provider);
        supportedEditor.ThinkingEffortOverride = AgentReasoningEffortLevel.Minimal;

        var definition = CreateDefinition(supportedEditor, [provider]);

        Assert.Equal(
            AgentReasoningEffortLevel.Minimal,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(definition.ConfigurationJson, "agent"));

        var unsupportedEditor = CreateEditor(provider);
        unsupportedEditor.ThinkingEffortOverride = AgentReasoningEffortLevel.None;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateDefinition(unsupportedEditor, [provider]));

        Assert.Contains("not supported", exception.Message, StringComparison.Ordinal);
        Assert.Contains("minimal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UsesProviderDefaultWhenAgentModelIsEmpty()
    {
        var provider = CreateProvider(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Luna);
        var editor = CreateEditor(provider);
        editor.Model = "  ";
        editor.ThinkingEffortOverride = AgentReasoningEffortLevel.Max;

        var definition = CreateDefinition(editor, [provider]);

        Assert.Equal(string.Empty, definition.Model);
        Assert.Equal(
            AgentReasoningEffortLevel.Max,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(definition.ConfigurationJson, "agent"));
    }

    private static AgentDefinition CreateDefinition(
        AgentEditorModel editor,
        IReadOnlyList<ProviderProfile> providers)
    {
        var catalog = new SandboxWorkspaceCatalog(
            Version: "1.0",
            Agents: [],
            Providers: providers,
            Capabilities: [],
            Memory: []);

        return AgentDefinitionFactory.Create(
            catalog,
            editor,
            Guid.NewGuid(),
            existingAgent: null,
            new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero),
            new ProviderProfileService(),
            "Save agent");
    }

    private static AgentEditorModel CreateEditor(ProviderProfile? provider)
    {
        return new AgentEditorModel
        {
            Name = "Thinking test agent",
            RoleTitle = "Tester",
            Summary = "Validates thinking effort persistence.",
            Instructions = "Test the configured behavior.",
            ProviderProfileId = provider?.Id,
            ConfigurationJson = "{}"
        };
    }

    private static ProviderProfile CreateProvider(ProviderKind kind, string defaultModel)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: $"{kind} provider",
            Kind: kind,
            BaseUrl: "http://provider.test",
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: defaultModel,
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
            SuggestedModels: [defaultModel])
        {
            IsPrivateProvider = ProviderPricingDefaults.IsPrivateProvider(kind),
            ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(kind, defaultModel)
        };
    }
}
