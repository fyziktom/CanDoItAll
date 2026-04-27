using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

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
    }

    [Fact]
    public void ResolveFeatureMatrix_does_not_claim_structured_output_for_openai_chat_completions()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.False(matrix.SupportsStructuredOutput);
        Assert.False(matrix.SupportsNativeWebSearch);
        Assert.False(matrix.SupportsHostedMcpServer);
        Assert.True(matrix.SupportsLocalMcpBridge);
        Assert.False(matrix.SupportsServiceManagedHistory);
    }

    [Fact]
    public void ResolveFeatureMatrix_limits_ollama_to_local_tool_bridge_capabilities()
    {
        var service = new ProviderProfileService();
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            supportsTools: true,
            preferFrameworkManagedHistory: true);

        var matrix = service.ResolveFeatureMatrix(provider);

        Assert.False(matrix.SupportsStructuredOutput);
        Assert.False(matrix.SupportsNativeWebSearch);
        Assert.False(matrix.SupportsHostedMcpServer);
        Assert.True(matrix.SupportsLocalMcpBridge);
        Assert.False(matrix.SupportsServiceManagedHistory);
        Assert.False(matrix.SupportsVision);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        ProviderTransportKind transport,
        bool supportsTools,
        bool preferFrameworkManagedHistory)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind == ProviderKind.Ollama ? "Remote Ollama" : "OpenAI",
            kind,
            kind == ProviderKind.Ollama ? "http://localhost:11434" : "https://api.openai.com/v1",
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
}
