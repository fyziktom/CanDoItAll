using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Mcp.Abstractions;

public enum McpServerDescriptorKind
{
    InternalHosted,
    LocalStdio,
    RemoteHttp
}

public enum McpApprovalMode
{
    NeverRequire,
    AlwaysRequire
}

public enum McpLifecycleOwner
{
    Application,
    LocalProcess,
    RemoteService
}

public enum McpStdioMessageFraming
{
    ContentLength,
    NewlineDelimitedJson
}

public enum McpTransportFailureKind
{
    InvalidJson,
    InvalidMessage,
    MessageTooLarge,
    DuplicateMessageId,
    InvalidMessageId,
    ExcessiveUnmatchedMessages,
    EndOfStream,
    ProcessExited,
    IoFailure
}

public abstract record McpServerDescriptor(
    McpServerDescriptorKind DescriptorKind,
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    IReadOnlySet<McpToolName> AllowedTools,
    McpApprovalMode ApprovalMode,
    McpLifecycleOwner LifecycleOwner,
    TimeSpan Timeout);

public sealed record InternalHostedMcpServerDescriptor(
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    IReadOnlySet<McpToolName> AllowedTools,
    McpApprovalMode ApprovalMode,
    TimeSpan Timeout,
    ImplementationKey ImplementationKey)
    : McpServerDescriptor(
        McpServerDescriptorKind.InternalHosted,
        Identity,
        ServerKey,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState,
        AllowedTools,
        ApprovalMode,
        McpLifecycleOwner.Application,
        Timeout);

public sealed record LocalStdioMcpServerDescriptor(
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    IReadOnlySet<McpToolName> AllowedTools,
    McpApprovalMode ApprovalMode,
    TimeSpan Timeout,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    McpStdioMessageFraming MessageFraming,
    IReadOnlySet<string> AllowedWorkingDirectories,
    IReadOnlyDictionary<string, string> EnvironmentVariableBindings,
    IReadOnlyDictionary<string, string> RawEnvironmentVariables)
    : McpServerDescriptor(
        McpServerDescriptorKind.LocalStdio,
        Identity,
        ServerKey,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState,
        AllowedTools,
        ApprovalMode,
        McpLifecycleOwner.LocalProcess,
        Timeout);

public sealed record RemoteHttpMcpServerDescriptor(
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    IReadOnlySet<McpToolName> AllowedTools,
    McpApprovalMode ApprovalMode,
    TimeSpan Timeout,
    Uri Endpoint,
    IReadOnlyDictionary<string, string> HeaderBindings,
    IReadOnlyDictionary<string, string> RawHeaders)
    : McpServerDescriptor(
        McpServerDescriptorKind.RemoteHttp,
        Identity,
        ServerKey,
        DisplayName,
        Description,
        Tags,
        OperationClassifications,
        SideEffectProfile,
        AvailabilityState,
        AllowedTools,
        ApprovalMode,
        McpLifecycleOwner.RemoteService,
        Timeout);

public sealed record McpToolDescriptor(
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    McpToolName ToolName,
    string DisplayName,
    string Description,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState);

/// <summary>
/// A tool reported by an MCP server during a setup test.
/// </summary>
/// <param name="Name">Name of the tool.</param>
/// <param name="Description">Description reported by the server; may be empty.</param>
/// <param name="InputSchema">JSON Schema of the tool's input as reported by the server, or null.</param>
public sealed record DiscoveredMcpTool(
    McpToolName Name,
    string Description,
    JsonElement? InputSchema = null);

/// <summary>
/// Result of an MCP server capability setup test.
/// </summary>
/// <param name="IsSuccess">
/// True when the definition was valid, the server started or connected, its tools were listed and matched the
/// allowed tools, and the server was stopped cleanly.
/// </param>
/// <param name="Identity">Kind and key of the tested capability, in the identity numbering.</param>
/// <param name="ServerKey">Key of the tested MCP server.</param>
/// <param name="CorrelationId">Correlation identifier of the test.</param>
/// <param name="DiscoveredTools">Tools the server reported; empty when listing did not happen.</param>
/// <param name="AllowedTools">Reported tools that the definition allows; empty when the test failed.</param>
/// <param name="Diagnostics">Problems found; empty when the test succeeded.</param>
/// <param name="CleanupCompleted">
/// True when the server was stopped cleanly after the test; false when stopping failed or the test ended before the
/// server was started.
/// </param>
public sealed record McpSetupTestResult(
    bool IsSuccess,
    CapabilityIdentity Identity,
    McpServerKey ServerKey,
    string CorrelationId,
    IReadOnlyList<DiscoveredMcpTool> DiscoveredTools,
    IReadOnlyList<DiscoveredMcpTool> AllowedTools,
    IReadOnlyList<CapabilityDiagnostic> Diagnostics,
    bool CleanupCompleted)
{
    public static McpSetupTestResult Success(
        McpServerDescriptor descriptor,
        string correlationId,
        IReadOnlyList<DiscoveredMcpTool> discoveredTools,
        IReadOnlyList<DiscoveredMcpTool> allowedTools,
        bool cleanupCompleted)
    {
        return new(true, descriptor.Identity, descriptor.ServerKey, correlationId, discoveredTools, allowedTools, [], cleanupCompleted);
    }

    public static McpSetupTestResult Failure(
        McpServerDescriptor descriptor,
        string correlationId,
        IReadOnlyList<CapabilityDiagnostic> diagnostics,
        IReadOnlyList<DiscoveredMcpTool>? discoveredTools = null,
        bool cleanupCompleted = false)
    {
        return new(false, descriptor.Identity, descriptor.ServerKey, correlationId, discoveredTools ?? [], [], diagnostics, cleanupCompleted);
    }
}

public interface IMcpRuntimeClient
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken);

    Task<string> CallToolAsync(
        McpToolName toolName,
        string jsonArguments,
        CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public interface IMcpClientFactory
{
    Task<IMcpRuntimeClient> CreateAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken);
}

public interface IMcpSetupTestService
{
    Task<McpSetupTestResult> TestAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken);
}
