using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderProfileEditorModel
{
    public string InstanceId { get; set; } = "provider.new-memory";

    public string DisplayName { get; set; } = "New memory provider";

    public MemoryProviderDriverKind DriverKind { get; set; } = MemoryProviderDriverKind.Mock;

    public bool IsEnabled { get; set; } = true;

    public MemoryProviderHealthState HealthState { get; set; } = MemoryProviderHealthState.Unknown;

    public MemoryProviderWorkspaceScope WorkspaceScope { get; set; } = MemoryProviderWorkspaceScope.AllWorkspaces;

    public MemoryProviderFallbackBehavior FallbackBehavior { get; set; } = MemoryProviderFallbackBehavior.DenyImplicitFallback;

    public string ProviderKind { get; set; } = "memory.mock";

    public bool SupportsContextQuerySync { get; set; } = true;

    public bool SupportsContextQueryAsync { get; set; }

    public bool SupportsSnapshotIngestion { get; set; }

    public bool SupportsProviderRequestedSources { get; set; }

    public bool SupportsImmediateFeedback { get; set; }

    public bool SupportsDelayedFeedback { get; set; }

    public bool SupportsProviderEvents { get; set; }

    public bool SupportsHostEventPolling { get; set; }

    public bool SupportsOperationStatus { get; set; }

    public bool SupportsRclUi { get; set; }

    public bool SupportsIframeUi { get; set; }

    public string ProviderUiUrl { get; set; } = string.Empty;

    public List<string> SelectionTags { get; set; } = [];

    public MemoryProviderHttpTransportEditorModel Http { get; set; } = new();

    public MemoryProviderMcpTransportEditorModel Mcp { get; set; } = new();

    public IReadOnlyList<MemoryCapabilityDescriptor> PreservedCapabilities { get; set; } = [];

    public IReadOnlyList<MemoryProviderUiSurface> PreservedUiSurfaces { get; set; } = [];

    public MemoryExtensionData PreservedExtensions { get; set; } = MemoryExtensionData.Empty;

    public MemoryProviderLimits PreservedLimits { get; set; } = MemoryProviderLimits.Default;

    public MemoryProtocolVersion PreservedProtocolVersion { get; set; } = MemoryProtocolVersion.Current;

    public MemoryProviderInteractionSupport? PreservedInteractionSupport { get; set; }

    public IReadOnlyList<string> LegacyRawCredentialKeys { get; set; } = [];

    public string HttpBaseUrl { get => Http.BaseUrl; set => Http.BaseUrl = value; }

    public string HttpQueryPath { get => Http.QueryPath; set => Http.QueryPath = value; }

    public string HttpHealthPath { get => Http.HealthPath; set => Http.HealthPath = value; }

    public string HttpApiKeyEnvironmentVariable { get => Http.ApiKeyEnvironmentVariable; set => Http.ApiKeyEnvironmentVariable = value; }

    public string HttpAuthHeaderName { get => Http.AuthHeaderName; set => Http.AuthHeaderName = value; }

    public string HttpAuthScheme { get => Http.AuthScheme; set => Http.AuthScheme = value; }

    public int HttpTimeoutMilliseconds { get => Http.TimeoutMilliseconds; set => Http.TimeoutMilliseconds = value; }

    public int HttpMaxRetryAttempts { get => Http.MaxRetryAttempts; set => Http.MaxRetryAttempts = value; }

    public string McpDescriptorKind { get => Mcp.DescriptorKind; set => Mcp.DescriptorKind = value; }

    public string McpServerKey { get => Mcp.ServerKey; set => Mcp.ServerKey = value; }

    public string McpDisplayName { get => Mcp.DisplayName; set => Mcp.DisplayName = value; }

    public string McpDescription { get => Mcp.Description; set => Mcp.Description = value; }

    public string McpRemoteEndpoint { get => Mcp.RemoteEndpoint; set => Mcp.RemoteEndpoint = value; }

    public string McpImplementationKey { get => Mcp.ImplementationKey; set => Mcp.ImplementationKey = value; }

    public string McpAuthHeaderName { get => Mcp.AuthHeaderName; set => Mcp.AuthHeaderName = value; }

    public string McpAuthHeaderEnvironmentVariable { get => Mcp.AuthHeaderEnvironmentVariable; set => Mcp.AuthHeaderEnvironmentVariable = value; }

    public string McpContextQueryTool { get => Mcp.ContextQueryTool; set => Mcp.ContextQueryTool = value; }

    public string McpIngestionTool { get => Mcp.IngestionTool; set => Mcp.IngestionTool = value; }

    public string McpSourceRequestTool { get => Mcp.SourceRequestTool; set => Mcp.SourceRequestTool = value; }

    public string McpFeedbackTool { get => Mcp.FeedbackTool; set => Mcp.FeedbackTool = value; }

    public string McpOperationStatusTool { get => Mcp.OperationStatusTool; set => Mcp.OperationStatusTool = value; }

    public static MemoryProviderProfileEditorModel FromProfile(MemoryProviderManagementProfile? profile) =>
        MemoryProviderProfileEditorMapper.Default.FromProfile(profile);
}

public sealed class MemoryProviderHttpTransportEditorModel
{
    public string BaseUrl { get; set; } = string.Empty;

    public string QueryPath { get; set; } = HttpMemoryProviderEndpoints.Query;

    public string HealthPath { get; set; } = HttpMemoryProviderEndpoints.Health;

    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public string AuthHeaderName { get; set; } = "Authorization";

    public string AuthScheme { get; set; } = "Bearer";

    public int TimeoutMilliseconds { get; set; } = 30_000;

    public int MaxRetryAttempts { get; set; }
}

public sealed class MemoryProviderMcpTransportEditorModel
{
    public string DescriptorKind { get; set; } = McpMemoryProviderDescriptorKinds.RemoteHttp;

    public string ServerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RemoteEndpoint { get; set; } = string.Empty;

    public string ImplementationKey { get; set; } = string.Empty;

    public string AuthHeaderName { get; set; } = "Authorization";

    public string AuthHeaderEnvironmentVariable { get; set; } = string.Empty;

    public string ContextQueryTool { get; set; } = string.Empty;

    public string IngestionTool { get; set; } = string.Empty;

    public string SourceRequestTool { get; set; } = string.Empty;

    public string FeedbackTool { get; set; } = string.Empty;

    public string OperationStatusTool { get; set; } = string.Empty;
}
