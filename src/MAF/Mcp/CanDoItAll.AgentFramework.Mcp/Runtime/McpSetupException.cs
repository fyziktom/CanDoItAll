using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed class McpSetupException(
    CapabilityDiagnosticCategory category,
    string fieldPath,
    string detail,
    string repairHint,
    int? httpStatusCode = null) : Exception(detail)
{
    public CapabilityDiagnosticCategory Category { get; } = category;

    public string FieldPath { get; } = fieldPath;

    public string Detail { get; } = detail;

    public string RepairHint { get; } = repairHint;

    public int? HttpStatusCode { get; } = httpStatusCode;
}
