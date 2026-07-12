using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class McpSetupValidator
{
    public static McpSetupTestResult? ValidateDescriptor(
        McpServerDescriptor descriptor,
        string correlationId)
    {
        if (descriptor.AvailabilityState != CapabilityAvailabilityState.Available)
        {
            return McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CapabilityUnavailable,
                "$.availabilityState",
                $"MCP server '{descriptor.ServerKey}' is {descriptor.AvailabilityState}.",
                "Enable or replace the MCP server before setup testing.");
        }

        if (descriptor.AllowedTools.Count == 0 &&
            descriptor is LocalStdioMcpServerDescriptor)
        {
            return McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.TemplateValidation,
                "$.allowedTools",
                $"Local MCP server '{descriptor.ServerKey}' must declare at least one allowed tool before launch.",
                "Run setup discovery or add explicit allowedTools before enabling local stdio MCP.");
        }

        if (descriptor is LocalStdioMcpServerDescriptor local)
        {
            return ValidateLocalDescriptor(local, correlationId);
        }

        if (descriptor is RemoteHttpMcpServerDescriptor remote &&
            remote.RawHeaders.Count > 0)
        {
            return McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SecretBinding,
                "$.headers",
                $"Remote MCP server '{descriptor.ServerKey}' persists raw headers.",
                "Replace raw headers with headerBindings.");
        }

        return null;
    }

    public static McpSetupTestResult? ValidateAllowedTools(
        McpServerDescriptor descriptor,
        string correlationId,
        IReadOnlyList<DiscoveredMcpTool> discoveredTools)
    {
        var discoveredToolNames = discoveredTools
            .Select(tool => tool.Name)
            .ToHashSet();
        var missingTools = descriptor.AllowedTools
            .Where(tool => !discoveredToolNames.Contains(tool))
            .ToArray();
        if (missingTools.Length == 0)
        {
            return null;
        }

        return McpSetupFailureFactory.Create(
            descriptor,
            correlationId,
            CapabilityDiagnosticCategory.McpListTools,
            "$.allowedTools",
            $"MCP server '{descriptor.ServerKey}' did not expose allowed tool(s): {string.Join(", ", missingTools.Select(tool => tool.Value))}.",
            "Update allowedTools to match discovered tools or repair the MCP server list-tools response.",
            discoveredTools);
    }

    private static McpSetupTestResult? ValidateLocalDescriptor(
        LocalStdioMcpServerDescriptor descriptor,
        string correlationId)
    {
        if (descriptor.RawEnvironmentVariables.Count > 0)
        {
            return McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SecretBinding,
                "$.environmentVariables",
                $"Local MCP server '{descriptor.ServerKey}' persists raw environment variables.",
                "Replace raw environment variables with environmentVariableBindings.");
        }

        if (!LocalMcpCommandPolicy.IsAllowed(descriptor.Command))
        {
            return McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CommandPolicy,
                "$.command",
                $"Local MCP command '{descriptor.Command}' is outside the approved command policy.",
                $"Use an approved command. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
        }

        return null;
    }
}
