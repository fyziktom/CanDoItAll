namespace CanDoItAll.AgentFramework.Models;

public enum ProviderNativeToolFamily
{
    CodeInterpreter,
    FileSearch,
    WebSearch,
    HostedMcpServer
}

public sealed record ProviderFeatureMatrix(
    ProviderKind Kind,
    ProviderTransportKind Transport,
    bool SupportsStreaming,
    bool SupportsTools,
    bool SupportsStructuredOutput,
    bool SupportsToolApprovalWrappers,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    bool SupportsNativeCodeInterpreter,
    bool SupportsNativeFileSearch,
    bool SupportsNativeWebSearch,
    bool SupportsHostedMcpServer,
    bool SupportsLocalMcpBridge,
    bool SupportsServiceManagedHistory,
    bool SupportsVision,
    bool SupportsCompaction,
    string GitHubCopilotRecommendation,
    bool SupportsFunctionTools = false,
    bool SupportsRunAsyncTypedOutput = false,
    bool SupportsResponseFormatJsonSchema = false,
    bool SupportsToolApprovalRequests = false,
    bool SupportsApprovalRequiredAIFunction = false,
    bool SupportsHostedTools = false,
    bool SupportsHostedMcp = false,
    bool SupportsLocalMcp = false);

public sealed record ProviderFeatureSupportResult(
    ProviderNativeToolFamily Family,
    bool IsSupported,
    string Summary,
    string Remediation);

public static class ProviderNativeToolKeys
{
    public const string CodeInterpreter = "provider_native_code_interpreter";
    public const string FileSearch = "provider_native_file_search";
    public const string WebSearch = "provider_native_web_search";

    public static bool TryResolveFamily(string? toolKey, out ProviderNativeToolFamily family)
    {
        family = default;
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            return false;
        }

        return toolKey.Trim() switch
        {
            CodeInterpreter or "provider-native-code-interpreter" => Assign(ProviderNativeToolFamily.CodeInterpreter, out family),
            FileSearch or "provider-native-file-search" => Assign(ProviderNativeToolFamily.FileSearch, out family),
            WebSearch or "provider-native-web-search" => Assign(ProviderNativeToolFamily.WebSearch, out family),
            _ => false
        };
    }

    public static string GetDisplayName(ProviderNativeToolFamily family)
    {
        return family switch
        {
            ProviderNativeToolFamily.CodeInterpreter => "provider-native code interpreter",
            ProviderNativeToolFamily.FileSearch => "provider-native file search",
            ProviderNativeToolFamily.WebSearch => "provider-native web search",
            ProviderNativeToolFamily.HostedMcpServer => "provider-native hosted MCP",
            _ => family.ToString()
        };
    }

    private static bool Assign(ProviderNativeToolFamily familyValue, out ProviderNativeToolFamily family)
    {
        family = familyValue;
        return true;
    }
}

public sealed record ProviderProfile(
    Guid Id,
    string Name,
    ProviderKind Kind,
    string BaseUrl,
    string ApiKeyEnvironmentVariable,
    string DefaultModel,
    ProviderTransportKind Transport,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsTools,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    string ConfigurationJson,
    string Notes,
    string HealthStatus,
    DateTimeOffset? LastCheckedAtUtc,
    IReadOnlyList<string> SuggestedModels);

public sealed record ProviderHealthResult(
    bool Success,
    string Summary,
    IReadOnlyList<string> SuggestedModels);
