using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderFeatureMatrixTests
{
    [Fact]
    public void ResolveFeatureMatrix_marks_openai_responses_as_structured_output_and_native_tool_capable()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            supportsTools: true,
            preferFrameworkManagedHistory: false);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsStructuredOutput);
        Assert.True(matrix.SupportsNativeWebSearch);
        Assert.True(matrix.SupportsHostedMcpServer);
        Assert.True(matrix.SupportsLocalMcpBridge);
        Assert.True(matrix.SupportsServiceManagedHistory);
        Assert.True(matrix.SupportsVision);
        Assert.True(matrix.SupportsCompaction);
        Assert.True(matrix.SupportsFunctionTools);
        Assert.True(matrix.SupportsResponseFormatJsonSchema);
        Assert.True(matrix.SupportsToolApprovalRequests);
        Assert.True(matrix.SupportsApprovalRequiredAIFunction);

        var constrained = service.ResolveFeatureMatrix(provider with
        {
            FeatureConstraints = new ProviderFeatureConstraints(
                AllowsStructuredOutput: false,
                AllowsVision: false,
                AllowsNativeTools: false,
                AllowsHostedMcp: false,
                AllowsServiceManagedHistory: false,
                AllowsCompaction: false,
                AllowsParallelFunctionTools: false)
        });

        Assert.False(constrained.SupportsStructuredOutput);
        Assert.False(constrained.SupportsResponseFormatJsonSchema);
        Assert.False(constrained.SupportsRunAsyncTypedOutput);
        Assert.False(constrained.SupportsVision);
        Assert.False(constrained.SupportsNativeCodeInterpreter);
        Assert.False(constrained.SupportsNativeFileSearch);
        Assert.False(constrained.SupportsNativeWebSearch);
        Assert.False(constrained.SupportsHostedMcpServer);
        Assert.False(constrained.SupportsHostedTools);
        Assert.False(constrained.SupportsHostedMcp);
        Assert.False(constrained.SupportsServiceManagedHistory);
        Assert.False(constrained.SupportsCompaction);
        Assert.True(constrained.SupportsFunctionTools);
        Assert.True(constrained.SupportsLocalMcpBridge);
        Assert.False(constrained.SupportsParallelFunctionTools);
    }

    [Fact]
    public void AudioCapabilityPolicyRejectsSourceProfilesAndPreservesPersonalProfiles()
    {
        var personal = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            supportsTools: false,
            preferFrameworkManagedHistory: true);
        var sourceManaged = personal with
        {
            Id = Guid.Parse("c45b659d-3054-465a-ad48-91049c458902"),
            CredentialBinding = new ProviderCredentialBinding(
                Guid.Parse("9f247304-1c4f-4404-a1d9-18d2488f2b05"),
                ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source,
                Guid.Parse("b647c53c-4dc0-4055-b46d-19c44fd9ed5d"))
        };

        Assert.True(ProviderAudioCapabilityPolicy.IsAvailable(personal));
        Assert.False(ProviderAudioCapabilityPolicy.IsAvailable(sourceManaged));
        ProviderAudioCapabilityPolicy.EnsureAvailable(
            personal,
            AgentProviderOperationKind.TranscribeSpeech);
        var exception = Assert.Throws<ProviderAudioCapabilityException>(() =>
            ProviderAudioCapabilityPolicy.EnsureAvailable(
                sourceManaged,
                AgentProviderOperationKind.SynthesizeSpeech));
        Assert.Equal(sourceManaged.Id, exception.ProviderProfileId);
        Assert.Equal(AgentProviderOperationKind.SynthesizeSpeech, exception.Operation);
        Assert.Equal(ProviderAudioCapabilityException.PublicMessage, exception.Message);

        var eligibleVoiceProviders = new[] { personal, sourceManaged }
            .Where(AgentVoiceSettingsPanel.IsOpenAiVoiceProvider)
            .ToArray();
        Assert.Equal(personal, Assert.Single(eligibleVoiceProviders));
        Assert.Equal(
            personal.Id.ToString("D"),
            AgentVoiceSettingsPanel.ResolveVoiceProviderIdText(
                currentProviderId: null,
                eligibleVoiceProviders));
        Assert.Equal(
            string.Empty,
            AgentVoiceSettingsPanel.ResolveVoiceProviderIdText(
                sourceManaged.Id,
                eligibleVoiceProviders));
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_openai_chat_completions_as_structured_output_with_maf_approval()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsStructuredOutput);
        Assert.True(matrix.SupportsResponseFormatJsonSchema);
        Assert.False(matrix.SupportsNativeWebSearch);
        Assert.False(matrix.SupportsHostedMcpServer);
        Assert.True(matrix.SupportsLocalMcpBridge);
        Assert.False(matrix.SupportsServiceManagedHistory);
        Assert.True(matrix.SupportsToolApprovalRequests);
        Assert.True(matrix.SupportsApprovalRequiredAIFunction);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_azure_chat_completions_as_structured_output_with_maf_approval()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsStructuredOutput);
        Assert.True(matrix.SupportsResponseFormatJsonSchema);
        Assert.True(matrix.SupportsFunctionTools);
        Assert.True(matrix.SupportsToolApprovalRequests);
        Assert.True(matrix.SupportsApprovalRequiredAIFunction);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_ollama_as_structured_output_and_local_tool_bridge_capable()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsStructuredOutput);
        Assert.False(matrix.SupportsNativeWebSearch);
        Assert.False(matrix.SupportsHostedMcpServer);
        Assert.True(matrix.SupportsLocalMcpBridge);
        Assert.False(matrix.SupportsServiceManagedHistory);
        Assert.False(matrix.SupportsVision);
        Assert.True(matrix.SupportsResponseFormatJsonSchema);
        Assert.False(matrix.SupportsToolApprovalRequests);
        Assert.False(matrix.SupportsApprovalRequiredAIFunction);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_ollama_vision_model_as_vision_capable()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true) with
        {
            DefaultModel = "gemma4:12b",
            SuggestedModels = ["gemma4:12b"]
        };

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsVision);
        Assert.True(matrix.SupportsFunctionTools);
        Assert.True(matrix.SupportsStructuredOutput);
        Assert.False(matrix.SupportsNativeWebSearch);
    }

    [Fact]
    public void ResolveFeatureMatrixForModel_uses_selected_model_instead_of_provider_suggestions()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true) with
        {
            DefaultModel = "gptoss32k:latest",
            SuggestedModels = ["gptoss32k:latest", "gemma4:12b"]
        };

        var defaultMatrix = service.ResolveFeatureMatrix(provider);
        var textModelMatrix = service.ResolveFeatureMatrixForModel(provider, "gptoss32k:latest");
        var visionModelMatrix = service.ResolveFeatureMatrixForModel(provider, "gemma4:12b");
        var qwenVisionModelMatrix = service.ResolveFeatureMatrixForModel(provider, "qwen3.5:2b");

        Assert.True(defaultMatrix.SupportsVision);
        Assert.False(textModelMatrix.SupportsVision);
        Assert.True(visionModelMatrix.SupportsVision);
        Assert.True(qwenVisionModelMatrix.SupportsVision);
    }

    [Fact]
    public void ResolveFeatureMatrixForModel_does_not_let_ollama_provider_wide_vision_metadata_override_text_model()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true) with
        {
            DefaultModel = "gptoss32k:latest",
            SuggestedModels = ["gptoss32k:latest", "qwen3.5:9b"],
            ConfigurationJson = """{"supportsVision":true}""",
            Tags = ["chat", "vision"]
        };

        var providerSummary = service.ResolveFeatureMatrix(provider);
        var textModelMatrix = service.ResolveFeatureMatrixForModel(provider, "gptoss32k:latest");
        var visionModelMatrix = service.ResolveFeatureMatrixForModel(provider, "qwen3.5:9b");

        Assert.True(providerSummary.SupportsVision);
        Assert.False(textModelMatrix.SupportsVision);
        Assert.True(visionModelMatrix.SupportsVision);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_configured_ollama_provider_as_vision_capable()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true) with
        {
            ConfigurationJson = """{"supportsVision":true}""",
            Tags = ["multimodal"]
        };

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.True(matrix.SupportsVision);
        Assert.True(matrix.SupportsStructuredOutput);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_image_generation_provider_by_explicit_purpose()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            supportsTools: false,
            preferFrameworkManagedHistory: false) with
        {
            DefaultModel = "gpt-image-1-mini",
            Purpose = ProviderProfilePurpose.ImageGeneration
        };

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.Equal(ProviderProfilePurpose.ImageGeneration, matrix.Purpose);
        Assert.True(matrix.SupportsImageGeneration);
        Assert.False(matrix.SupportsTools);
        Assert.False(matrix.SupportsNativeWebSearch);
        Assert.False(matrix.SupportsLocalMcpBridge);
    }

    [Fact]
    public void ResolveFeatureMatrix_marks_comfyui_as_private_image_provider_without_chat_tools()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.ComfyUi,
            ProviderTransportKind.ChatCompletions,
            supportsTools: false,
            preferFrameworkManagedHistory: true) with
        {
            DefaultModel = "comfyui-workflow",
            Purpose = ProviderProfilePurpose.ImageGeneration
        };

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.Equal(ProviderKind.ComfyUi, matrix.Kind);
        Assert.True(matrix.SupportsImageGeneration);
        Assert.False(matrix.SupportsStructuredOutput);
        Assert.False(matrix.SupportsFunctionTools);
        Assert.False(matrix.SupportsVision);
        Assert.False(matrix.SupportsToolApprovalRequests);
    }

    [Fact]
    public void Persisted_provider_registry_uses_feature_matrix_and_transport_metadata()
    {
        var source = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "Administration",
            "DatabaseProviderProfileRegistry.cs");
        var metadataSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "RuntimeProjection",
            "ProviderMetadata.cs");
        var mapperSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "RuntimeProjection",
            "PersistedProviderProfileMapper.cs");

        Assert.DoesNotContain("SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses", source, StringComparison.Ordinal);
        Assert.Contains("capabilityProfile.Transport", source, StringComparison.Ordinal);
        Assert.Contains("ResolveFeatureMatrix", source, StringComparison.Ordinal);
        Assert.Contains("entity.SupportsVision = featureMatrix.SupportsVision;", source, StringComparison.Ordinal);
        Assert.Contains("ResolveTransport", mapperSource, StringComparison.Ordinal);
        Assert.Contains("ProviderTransportKind transport", metadataSource, StringComparison.Ordinal);
        Assert.Contains("supportsVision", metadataSource, StringComparison.Ordinal);
        Assert.Contains("ComfyUiProviderAdministrationConnector.PluginKey", mapperSource, StringComparison.Ordinal);
        Assert.Contains("ProviderProfilePurpose.ImageGeneration", mapperSource, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkProviderKind.ComfyUi", metadataSource, StringComparison.Ordinal);
        Assert.Contains("ProviderModelCatalogPolicy.Resolve", mapperSource, StringComparison.Ordinal);
        Assert.Contains("ProviderMetadata.ReadSuggestedModels(provider)", mapperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagedSeedProviderFallbacks.OpenAiSuggestedModels", mapperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_settings_ui_is_authoritative_in_agent_framework_and_workspace_only_selects_default()
    {
        var settingsPageSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.Workspace",
            "Pages",
            "SettingsPage.razor.cs");
        var settingsPageMarkup = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.Workspace",
            "Pages",
            "SettingsPage.razor");
        var providerPanelSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "AgentProviderProfilesPanel.razor.cs");
        var providerPanelMarkup = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "AgentProviderProfilesPanel.razor");
        var providerExecutionSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "Administration",
            "ProviderAdministrationConnectors.cs");

        Assert.DoesNotContain("ProviderAdministrationService", settingsPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("providerModel", settingsPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Provider editor", settingsPageMarkup, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceProviderCatalog", settingsPageMarkup, StringComparison.Ordinal);
        Assert.Contains("/agents?tab=providers", settingsPageSource, StringComparison.Ordinal);
        Assert.Contains("IProviderRuntimeAdministrationService", providerPanelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentFrameworkWorkspaceService", providerPanelSource, StringComparison.Ordinal);
        Assert.Contains("ProviderModelPricingEditor", providerPanelMarkup, StringComparison.Ordinal);
        Assert.Contains("ComfyUiWorkflowTemplateJson", providerExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ConnectorConfigFieldType.Json", providerExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ComfyUiWorkflowTemplatePath", providerExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ComfyUiPositivePromptNodeId", providerExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ComfyUiPollIntervalMilliseconds", providerExecutionSource, StringComparison.Ordinal);
        Assert.Contains("ConnectorAgentExposure(\"image_generation\"", providerExecutionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Agent_framework_provider_mapping_and_workflow_image_selection_do_not_fallback_to_chat_providers()
    {
        var registrySource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "Administration",
            "DatabaseProviderProfileRegistry.cs");
        var mapperSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "RuntimeProjection",
            "PersistedProviderProfileMapper.cs");
        var metadataSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework.ProviderManagement",
            "RuntimeProjection",
            "ProviderMetadata.cs");
        var workflowRendererSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowImageGenerationSettingsRenderer.razor");
        var treeNodeBuilderSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "ProviderProfileTreeNodeBuilder.cs");
        var voiceSettingsSource = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "AgentVoiceSettingsPanel.razor");
        var openAiDriverSource = ReadRepositoryFile(
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Providers",
            "Drivers",
            "OpenAiProviderDriver.cs");

        Assert.Contains("ResolveMappedProviderKind", mapperSource, StringComparison.Ordinal);
        Assert.Contains("No AgentFramework provider kind mapping exists for connector plugin", mapperSource, StringComparison.Ordinal);
        Assert.Contains("No AgentFramework provider transport mapping exists for connector plugin", mapperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_ => AgentFrameworkProviderKind.Ollama", mapperSource, StringComparison.Ordinal);
        Assert.Contains("providerProfileService.CreateProfile", registrySource, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkProviderKind.ComfyUi => ComfyUiProviderAdministrationConnector.PluginKey", metadataSource, StringComparison.Ordinal);
        Assert.Contains("No workspace connector plugin mapping exists for provider kind", metadataSource, StringComparison.Ordinal);

        Assert.Contains("option.Purpose == ProviderProfilePurpose.ImageGeneration", workflowRendererSource, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!provider.IsEnabled)\"", workflowRendererSource, StringComparison.Ordinal);
        Assert.Contains("not an available image-generation provider", workflowRendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderProfilePurpose.Chat", workflowRendererSource, StringComparison.Ordinal);
        Assert.Contains("ProviderKind.ComfyUi => \"image\"", treeNodeBuilderSource, StringComparison.Ordinal);
        Assert.Contains("ProviderAudioCapabilityPolicy.IsAvailable(provider)", voiceSettingsSource, StringComparison.Ordinal);
        Assert.Contains("ProviderAudioCapabilityPolicy.EnsureAvailable", openAiDriverSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_settings_ui_does_not_expose_non_persisted_batch_controls()
    {
        var settingsPageMarkup = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.Workspace",
            "Pages",
            "SettingsPage.razor");
        var providerPanelMarkup = ReadRepositoryFile(
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "AgentProviderProfilesPanel.razor");
        var providerDispatchModels = ReadRepositoryFile(
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Models",
            "Providers",
            "ProviderDispatchModels.cs");

        Assert.Contains("SupportsBatching", providerDispatchModels, StringComparison.Ordinal);
        Assert.Contains("MaxBatchSize", providerDispatchModels, StringComparison.Ordinal);
        Assert.DoesNotContain("Max batch size", settingsPageMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Max batch size", providerPanelMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Max queue delay", settingsPageMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Max queue delay", providerPanelMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_bootstrap_provider_uses_managed_openai_responses_defaults()
    {
        var source = ReadRepositoryFile(
            "src",
            "App",
            "CanDoItAll.Composition",
            "RuntimeHostServiceCollectionExtensions.cs");

        Assert.Contains("ManagedSeedProviderFallbacks.OpenAiDefaultProviderName", source, StringComparison.Ordinal);
        Assert.Contains("SupportsStructuredOutput = true", source, StringComparison.Ordinal);
        Assert.Contains("ProviderTransportKind.Responses", source, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.SupportsStructuredOutput = false", source, StringComparison.Ordinal);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        ProviderTransportKind transport,
        bool supportsTools,
        bool preferFrameworkManagedHistory)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind == ProviderKind.Ollama ? "Remote Ollama" : kind == ProviderKind.AzureOpenAi ? "Azure OpenAI" : "OpenAI",
            kind,
            kind == ProviderKind.Ollama ? "http://localhost:11434" : kind == ProviderKind.AzureOpenAi ? "https://example.openai.azure.com" : "https://api.openai.com/v1",
            kind == ProviderKind.Ollama ? string.Empty : "OPENAI_API_KEY",
            kind == ProviderKind.Ollama ? "llama3.1" : "gpt-5.4",
            transport,
            true,
            true,
            supportsTools,
            preferFrameworkManagedHistory,
            false,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. pathParts]));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
