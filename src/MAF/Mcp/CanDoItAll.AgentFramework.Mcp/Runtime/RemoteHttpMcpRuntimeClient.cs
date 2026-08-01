using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class RemoteHttpMcpRuntimeClient(
    RemoteHttpMcpServerDescriptor descriptor) : IMcpRuntimeClient
{
    private readonly McpOperationTimeout timeout = new(descriptor.Timeout);
    private McpClient? client;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return timeout.RunAsync(
            StartCoreAsync,
            "Remote MCP initialize handshake",
            cancellationToken);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (client is not null)
        {
            throw new InvalidOperationException("The remote MCP client has already been started.");
        }

        var transport = new HttpClientTransport(
            RemoteHttpMcpTransportOptionsFactory.Create(descriptor));
        try
        {
            client = await McpClient.CreateAsync(
                    transport,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw MapException(
                exception,
                CapabilityDiagnosticCategory.McpHandshake,
                "$.endpoint",
                "initialize");
        }
    }

    public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        return timeout.RunAsync(
            operationToken => ExecuteAsync<IReadOnlyList<DiscoveredMcpTool>>(
                async () =>
                {
                    var tools = await RequireClient()
                        .ListToolsAsync(cancellationToken: operationToken)
                        .ConfigureAwait(false);
                    return tools
                        .Select(tool => new DiscoveredMcpTool(
                            McpToolName.Create(tool.Name),
                            tool.Description ?? string.Empty,
                            tool.JsonSchema.Clone()))
                        .ToArray();
                },
                CapabilityDiagnosticCategory.McpListTools,
                "$.tools",
                "tools/list",
                operationToken),
            "Remote MCP tools/list request",
            cancellationToken);
    }

    public Task<string> CallToolAsync(
        McpToolName toolName,
        string jsonArguments,
        CancellationToken cancellationToken)
    {
        if (!descriptor.AllowedTools.Contains(toolName))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.AccessPolicy,
                "$.allowedTools",
                $"MCP tool '{toolName}' is outside the allowed tool set for '{descriptor.ServerKey}'.",
                "Add the tool to the descriptor allowlist only after reviewing its behavior and side effects.");
        }

        var arguments = McpToolArgumentsParser.Parse(jsonArguments, descriptor.ServerKey);
        return timeout.RunAsync(
            operationToken => ExecuteAsync(
                async () =>
                {
                    var result = await RequireClient()
                        .CallToolAsync(
                            toolName.Value,
                            arguments,
                            cancellationToken: operationToken)
                        .ConfigureAwait(false);
                    return McpToolResultReader.Read(
                        result,
                        descriptor.ServerKey,
                        toolName);
                },
                CapabilityDiagnosticCategory.RuntimeAdapter,
                "$.tools.call",
                $"tools/call:{toolName.Value}",
                operationToken),
            $"Remote MCP tools/call request for '{toolName.Value}'",
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var currentClient = client;
        client = null;
        if (currentClient is not null)
        {
            await currentClient.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw MapException(exception, category, fieldPath, operationName);
        }
    }

    private McpClient RequireClient()
    {
        return client ?? throw new InvalidOperationException(
            "The remote MCP client must be started before it can be used.");
    }

    private McpSetupException MapException(
        Exception exception,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string operationName)
    {
        if (FindException<TimeoutException>(exception) is not null ||
            FindException<OperationCanceledException>(exception) is not null)
        {
            return new McpSetupException(
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"Remote MCP operation '{operationName}' timed out for '{descriptor.ServerKey}'.",
                "Verify endpoint responsiveness before increasing the configured timeout.");
        }

        if (FindException<HttpRequestException>(exception) is { } httpException)
        {
            return new McpSetupException(
                httpException.StatusCode.HasValue
                    ? CapabilityDiagnosticCategory.HttpStatus
                    : category,
                fieldPath,
                httpException.StatusCode.HasValue
                    ? $"Remote MCP operation '{operationName}' returned HTTP {(int)httpException.StatusCode.Value} for '{descriptor.ServerKey}'."
                    : $"Remote MCP operation '{operationName}' could not reach '{descriptor.ServerKey}' at host '{descriptor.Endpoint.Host}'.",
                "Verify the endpoint, network path, and environment-backed credential binding.",
                httpException.StatusCode.HasValue ? (int)httpException.StatusCode.Value : null);
        }

        return new McpSetupException(
            exception is McpException ? category : CapabilityDiagnosticCategory.RuntimeAdapter,
            fieldPath,
            $"Remote MCP operation '{operationName}' failed for '{descriptor.ServerKey}' ({exception.GetType().Name}).",
            "Inspect the remote MCP protocol response and server logs using the correlation recorded by the caller.");
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is TException match)
            {
                return match;
            }

            current = current.InnerException;
        }

        return null;
    }
}
