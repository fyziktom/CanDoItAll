using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpSetupFailureFactory
{
    public static McpSetupTestResult Create(
        McpServerDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint,
        IReadOnlyList<DiscoveredMcpTool>? discoveredTools = null,
        bool cleanupCompleted = false,
        int? httpStatusCode = null)
    {
        return McpSetupTestResult.Failure(
            descriptor,
            correlationId,
            [
                McpDiagnostics.Create(
                    category,
                    descriptor,
                    fieldPath,
                    detail,
                    repairHint,
                    correlationId,
                    httpStatusCode)
            ],
            discoveredTools,
            cleanupCompleted);
    }
}
