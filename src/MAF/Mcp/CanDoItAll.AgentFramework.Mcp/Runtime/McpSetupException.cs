using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed class McpSetupException(
    CapabilityDiagnosticCategory category,
    string fieldPath,
    string detail,
    string repairHint,
    int? httpStatusCode = null,
    McpTransportFailureKind? transportFailure = null) : Exception(detail)
{
    public CapabilityDiagnosticCategory Category { get; } = category;

    public string FieldPath { get; } = fieldPath;

    public string Detail { get; } = detail;

    public string RepairHint { get; } = repairHint;

    public int? HttpStatusCode { get; } = httpStatusCode;

    public McpTransportFailureKind? TransportFailure { get; } = transportFailure;
}
