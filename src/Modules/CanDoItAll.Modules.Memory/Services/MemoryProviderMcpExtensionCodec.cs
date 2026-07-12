using CanDoItAll.Memory.Mcp;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderMcpExtensionCodec
{
    private static readonly Regex HeaderNamePattern = new(
        "^[!#$%&'*+\\-.^_`|~0-9A-Za-z]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly IReadOnlySet<string> ManagedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        McpMemoryProviderConfigurationKeys.DescriptorKind,
        McpMemoryProviderConfigurationKeys.ServerKey,
        McpMemoryProviderConfigurationKeys.DisplayName,
        McpMemoryProviderConfigurationKeys.Description,
        McpMemoryProviderConfigurationKeys.RemoteEndpoint,
        McpMemoryProviderConfigurationKeys.ImplementationKey,
        McpMemoryProviderConfigurationKeys.AuthHeaderName,
        McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable,
        McpMemoryProviderConfigurationKeys.ContextQueryTool,
        McpMemoryProviderConfigurationKeys.IngestionTool,
        McpMemoryProviderConfigurationKeys.SourceRequestTool,
        McpMemoryProviderConfigurationKeys.FeedbackTool,
        McpMemoryProviderConfigurationKeys.EventPollTool,
        McpMemoryProviderConfigurationKeys.OperationStatusTool
    };

    public static MemoryProviderMcpTransportEditorModel Read(IReadOnlyDictionary<string, JsonElement> values) =>
        new()
        {
            DescriptorKind = MemoryProviderExtensionValues.ReadString(
                values,
                McpMemoryProviderConfigurationKeys.DescriptorKind,
                McpMemoryProviderDescriptorKinds.RemoteHttp),
            ServerKey = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.ServerKey),
            DisplayName = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.DisplayName),
            Description = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.Description),
            RemoteEndpoint = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.RemoteEndpoint),
            ImplementationKey = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.ImplementationKey),
            AuthHeaderName = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.AuthHeaderName, "Authorization"),
            AuthHeaderEnvironmentVariable = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable),
            ContextQueryTool = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.ContextQueryTool),
            IngestionTool = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.IngestionTool),
            SourceRequestTool = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.SourceRequestTool),
            FeedbackTool = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.FeedbackTool),
            OperationStatusTool = MemoryProviderExtensionValues.ReadString(values, McpMemoryProviderConfigurationKeys.OperationStatusTool)
        };

    public static void Write(
        IDictionary<string, JsonElement> values,
        MemoryProviderMcpTransportEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!string.Equals(
                editor.DescriptorKind,
                McpMemoryProviderDescriptorKinds.RemoteHttp,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MCP memory providers currently support only the remote-http descriptor kind.");
        }

        if (!Uri.TryCreate(editor.RemoteEndpoint?.Trim(), UriKind.Absolute, out var endpoint) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException("MCP memory provider endpoint must be an absolute HTTP(S) URI without embedded credentials, query strings, or fragments.");
        }

        var isHttps = string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && endpoint.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            throw new InvalidOperationException("MCP memory provider endpoint must use HTTPS or loopback HTTP.");
        }

        var credential = MemoryProviderCredentialReference.ParseOptional(
            editor.AuthHeaderEnvironmentVariable,
            nameof(editor.AuthHeaderEnvironmentVariable));
        ValidateHeaderName(editor.AuthHeaderName);

        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.DescriptorKind, editor.DescriptorKind);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.ServerKey, editor.ServerKey);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.DisplayName, editor.DisplayName);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.Description, editor.Description);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.RemoteEndpoint, editor.RemoteEndpoint);
        MemoryProviderExtensionValues.SetString(
            values,
            McpMemoryProviderConfigurationKeys.AuthHeaderName,
            credential is null ? null : editor.AuthHeaderName);
        MemoryProviderExtensionValues.SetString(
            values,
            McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable,
            credential?.EnvironmentVariableName);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.ContextQueryTool, editor.ContextQueryTool);
        MemoryProviderExtensionValues.SetString(values, McpMemoryProviderConfigurationKeys.OperationStatusTool, editor.OperationStatusTool);
    }

    public static void RemoveManagedValues(IDictionary<string, JsonElement> values)
    {
        foreach (var key in ManagedKeys)
        {
            values.Remove(key);
        }
    }

    private static void ValidateHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !HeaderNamePattern.IsMatch(headerName.Trim()))
        {
            throw new ArgumentException("Authentication header must be a valid HTTP header name.", nameof(headerName));
        }
    }
}
