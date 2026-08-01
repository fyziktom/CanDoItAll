using System.Diagnostics;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpEnvironmentBinder
{
    public static void Apply(
        ProcessStartInfo startInfo,
        LocalStdioMcpServerDescriptor descriptor)
    {
        foreach (var (targetName, value) in descriptor.RawEnvironmentVariables)
        {
            if (string.IsNullOrWhiteSpace(targetName) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            startInfo.Environment[targetName.Trim()] = value;
        }

        foreach (var (targetName, sourceName) in descriptor.EnvironmentVariableBindings)
        {
            if (string.IsNullOrWhiteSpace(targetName) ||
                string.IsNullOrWhiteSpace(sourceName))
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.SecretBinding,
                    "$.environmentVariableBindings",
                    $"MCP server '{descriptor.ServerKey}' has an invalid environment variable binding.",
                    "Set each binding to a target environment variable name and a runtime source environment variable name.");
            }

            var value = Environment.GetEnvironmentVariable(sourceName.Trim());
            if (value is null)
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.SecretBinding,
                    $"$.environmentVariableBindings.{targetName}",
                    $"MCP server '{descriptor.ServerKey}' requires environment variable binding source '{sourceName}', but it is not set.",
                    "Set the source environment variable before running the setup test.");
            }

            startInfo.Environment[targetName.Trim()] = value;
        }
    }
}
