using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafAgentRuntimeImageAnalysisModelTests
{
    [Fact]
    public void ResolveProviderImageAnalysisModel_prefers_runtime_model_when_it_supports_vision()
    {
        var provider = CreateOllamaVisionProvider() with
        {
            DefaultModel = "qwen3.5:9b",
            SuggestedModels = ["qwen3.5:9b", "llama3.2:3b"]
        };

        var model = WorkspaceImageAnalysisModelResolver.ResolveProviderImageAnalysisModel(provider, "qwen3.5:2b");

        Assert.Equal("qwen3.5:2b", model);
    }

    [Fact]
    public void ResolveProviderImageAnalysisModel_uses_provider_vision_model_when_runtime_model_is_text_only()
    {
        var provider = CreateOllamaVisionProvider() with
        {
            DefaultModel = "qwen3.5:9b",
            SuggestedModels = ["qwen3.5:9b", "llama3.2:3b"],
            Tags = ["chat", "vision"],
            ConfigurationJson = """{"supportsVision":true}"""
        };

        var model = WorkspaceImageAnalysisModelResolver.ResolveProviderImageAnalysisModel(provider, "llama3.2:3b");

        Assert.Equal("qwen3.5:9b", model);
    }

    [Fact]
    public void ResolveProviderImageAnalysisModel_prefers_configured_vision_model_before_default_model()
    {
        var provider = CreateOllamaVisionProvider() with
        {
            DefaultModel = "qwen3.5:9b",
            SuggestedModels = ["qwen3.5:9b", "gemma4:12b"],
            ConfigurationJson = """{"visionModel":"gemma4:12b"}"""
        };

        var model = WorkspaceImageAnalysisModelResolver.ResolveProviderImageAnalysisModel(provider, "llama3.2:3b");

        Assert.Equal("gemma4:12b", model);
    }

    private static ProviderProfile CreateOllamaVisionProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Remote Ollama Vision",
            ProviderKind.Ollama,
            "http://localhost:11434",
            string.Empty,
            "qwen3.5:9b",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            true,
            false,
            "{}",
            string.Empty,
            "Not checked",
            null,
            ["qwen3.5:9b"]);
    }
}
