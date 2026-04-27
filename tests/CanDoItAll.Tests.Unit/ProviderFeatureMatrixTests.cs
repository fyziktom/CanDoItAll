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
        Assert.True(matrix.SupportsFunctionTools);
        Assert.True(matrix.SupportsResponseFormatJsonSchema);
        Assert.True(matrix.SupportsToolApprovalRequests);
        Assert.True(matrix.SupportsApprovalRequiredAIFunction);
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
        Assert.False(matrix.SupportsResponseFormatJsonSchema);
        Assert.False(matrix.SupportsToolApprovalRequests);
        Assert.False(matrix.SupportsApprovalRequiredAIFunction);
    }

    [Fact]
    public void Workspace_backed_provider_registry_uses_feature_matrix_and_transport_metadata()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.AgentFramework",
            "Providers",
            "WorkspaceBackedAgentProviderProfileRegistry.cs");
        var metadataSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.AgentFramework",
            "Providers",
            "AgentFrameworkProviderMetadata.cs");

        Assert.DoesNotContain("SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses", source, StringComparison.Ordinal);
        Assert.Contains("var selectedTransport = model.Transport;", source, StringComparison.Ordinal);
        Assert.Contains("ResolveFeatureMatrix", source, StringComparison.Ordinal);
        Assert.Contains("ResolveTransport", source, StringComparison.Ordinal);
        Assert.Contains("providerTransport", metadataSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Managed_sqlite_bootstrap_provider_matches_chat_completions_structured_output_support()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Composition",
            "RuntimeHostServiceCollectionExtensions.cs");

        Assert.Contains("Name = ManagedSqliteOpenAiProviderName", source, StringComparison.Ordinal);
        Assert.Contains("SupportsStructuredOutput = true", source, StringComparison.Ordinal);
        Assert.Contains("ProviderTransportKind.ChatCompletions", source, StringComparison.Ordinal);
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
