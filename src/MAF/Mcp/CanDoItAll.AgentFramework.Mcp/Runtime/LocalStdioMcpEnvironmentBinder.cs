using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpEnvironmentBinder
{
    public static IReadOnlyDictionary<string, string?> Build(
        LocalStdioMcpServerDescriptor descriptor)
    {
        var environmentPolicy = new WorkspaceCommandEnvironmentPolicy();
        var explicitValues = new Dictionary<string, string?>(
            environmentPolicy.EnvironmentNameComparer);
        foreach (var (targetName, value) in descriptor.RawEnvironmentVariables)
        {
            ValidateRuntimeEnvironmentVariableName(descriptor, targetName);

            if (!explicitValues.TryAdd(targetName, value))
            {
                throw InvalidBinding(descriptor, "ambiguous target");
            }
        }

        foreach (var (targetName, sourceName) in descriptor.EnvironmentVariableBindings)
        {
            ValidateEnvironmentVariableName(descriptor, targetName, "target");
            ValidateEnvironmentVariableName(descriptor, sourceName, "source");

            var value = Environment.GetEnvironmentVariable(sourceName);
            if (value is null)
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.SecretBinding,
                    $"$.environmentVariableBindings.{targetName}",
                    $"MCP server '{descriptor.ServerKey}' requires environment variable binding source '{sourceName}', but it is not set.",
                    "Set the source environment variable before running the setup test.");
            }

            if (!explicitValues.TryAdd(targetName, value))
            {
                throw InvalidBinding(descriptor, "ambiguous target");
            }
        }

        return environmentPolicy.MergeEnvironmentVariables(explicitValues, "local_mcp");
    }

    private static void ValidateEnvironmentVariableName(
        LocalStdioMcpServerDescriptor descriptor,
        string? name,
        string part)
    {
        if (!McpEnvironmentVariableNamePolicy.IsValid(name))
        {
            throw InvalidBinding(descriptor, part);
        }
    }

    private static void ValidateRuntimeEnvironmentVariableName(
        LocalStdioMcpServerDescriptor descriptor,
        string? name)
    {
        if (!McpEnvironmentVariableNamePolicy.IsValidRuntimeName(name))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.SecretBinding,
                "$.environmentVariables",
                $"MCP server '{descriptor.ServerKey}' received an invalid runtime environment variable name.",
                "Repair the runtime environment composition before starting the MCP server.");
        }
    }

    private static McpSetupException InvalidBinding(
        LocalStdioMcpServerDescriptor descriptor,
        string part)
        => new(
            CapabilityDiagnosticCategory.SecretBinding,
            "$.environmentVariableBindings",
            $"MCP server '{descriptor.ServerKey}' has an invalid {part} environment variable name in its bindings.",
            "Set each binding to a unique normalized target environment-variable name and a normalized runtime source environment-variable name.");
}
