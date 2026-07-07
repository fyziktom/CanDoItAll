using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentRuntimeCapabilityScopeOverride(
    IReadOnlyList<CapabilityAccessPolicy> Policies,
    IReadOnlyList<CapabilityIdentity> RequiredCapabilities,
    IReadOnlyList<AgentRuntimeRequiredToolReceipt>? RequiredReceipts = null)
{
    public static AgentRuntimeCapabilityScopeOverride Empty { get; } = new([], []);

    public IReadOnlyList<AgentRuntimeRequiredToolReceipt> RequiredReceipts { get; init; } = RequiredReceipts ?? [];

    public bool IsEmpty =>
        (Policies?.Count ?? 0) == 0 &&
        (RequiredCapabilities?.Count ?? 0) == 0 &&
        RequiredReceipts.Count == 0;
}

public sealed record AgentRuntimeRequiredToolReceipt(
    string Key,
    AgentRuntimeRequiredToolReceiptKind Kind,
    string ToolName,
    string RuntimeToolProviderKey,
    string McpServerKey,
    int MinimumCount,
    bool RequireSuccessfulExit,
    bool RequireCurrentRun,
    AgentRuntimeRequiredToolReceiptActivation Activation,
    string Reason);

[JsonConverter(typeof(JsonStringEnumConverter<AgentRuntimeRequiredToolReceiptKind>))]
public enum AgentRuntimeRequiredToolReceiptKind
{
    RuntimeToolName,
    RuntimeToolProviderKey,
    RuntimeToolNameWithProvider,
    McpToolName
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRuntimeRequiredToolReceiptActivation>))]
public enum AgentRuntimeRequiredToolReceiptActivation
{
    Always,
    WhenLaunchContextDeclaresTool
}
