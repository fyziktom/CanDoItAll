using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using ModelContextProtocol.Client;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class RemoteHttpMcpTransportOptionsFactory
{
    private const string HttpTokenSymbols = "!#$%&'*+-.^_`|~";

    public static HttpClientTransportOptions Create(
        RemoteHttpMcpServerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.RawHeaders.Count > 0)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.SecretBinding,
                "$.headers",
                $"Remote MCP server '{descriptor.ServerKey}' persists raw headers.",
                "Replace raw headers with environment-backed header bindings.");
        }

        return new HttpClientTransportOptions
        {
            Endpoint = descriptor.Endpoint,
            Name = descriptor.DisplayName,
            ConnectionTimeout = descriptor.Timeout,
            AdditionalHeaders = ResolveHeaders(descriptor)
        };
    }

    private static Dictionary<string, string> ResolveHeaders(
        RemoteHttpMcpServerDescriptor descriptor)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (headerName, environmentVariable) in descriptor.HeaderBindings)
        {
            ValidateBinding(descriptor, headerName, environmentVariable);
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(value) ||
                value.Contains('\r') ||
                value.Contains('\n'))
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.SecretBinding,
                    $"$.headerBindings.{headerName}",
                    $"Remote MCP credential environment variable '{environmentVariable}' is missing or contains an invalid header value for '{descriptor.ServerKey}'.",
                    "Set the configured credential environment variable to one non-empty HTTP header value before starting the MCP provider.");
            }

            if (!headers.TryAdd(headerName, value))
            {
                throw InvalidBinding(descriptor, "duplicate header name");
            }
        }

        return headers;
    }

    private static void ValidateBinding(
        RemoteHttpMcpServerDescriptor descriptor,
        string headerName,
        string environmentVariable)
    {
        if (!IsHttpHeaderName(headerName))
        {
            throw InvalidBinding(descriptor, "header name");
        }

        if (!IsEnvironmentVariableName(environmentVariable))
        {
            throw InvalidBinding(descriptor, "environment-variable reference");
        }
    }

    private static bool IsHttpHeaderName(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                HttpTokenSymbols.Contains(character));
    }

    private static bool IsEnvironmentVariableName(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            !(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.Skip(1).All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character == '_');
    }

    private static McpSetupException InvalidBinding(
        RemoteHttpMcpServerDescriptor descriptor,
        string part)
    {
        return new McpSetupException(
            CapabilityDiagnosticCategory.SecretBinding,
            "$.headerBindings",
            $"Remote MCP server '{descriptor.ServerKey}' has an invalid {part} in its header bindings.",
            "Configure normalized HTTP header names mapped to normalized runtime environment-variable names.");
    }
}
