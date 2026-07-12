using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpJsonRpcConnection(
    LocalStdioMcpServerDescriptor descriptor,
    LocalStdioMcpProcessSession session)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim requestGate = new(1, 1);
    private int nextRequestId;

    public async Task<JsonDocument> SendRequestAsync(
        string method,
        object parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        try
        {
            var process = session.RequireRunningProcess(
                failureCategory,
                failureFieldPath);
            var requestId = Interlocked.Increment(ref nextRequestId);
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(
                    parameters,
                    SerializerOptions)
            };
            await McpJsonRpcFraming.WriteMessageAsync(
                process.StandardInput.BaseStream,
                payload,
                descriptor.MessageFraming,
                cancellationToken);

            while (true)
            {
                var message = await session.ReadNextMessageAsync(
                    process,
                    failureCategory,
                    failureFieldPath,
                    cancellationToken);
                var document = LocalStdioMcpResponseParser.ParseMessage(
                    message,
                    descriptor.ServerKey,
                    failureCategory,
                    failureFieldPath);
                if (!LocalStdioMcpResponseParser.IsResponseForRequest(
                        document.RootElement,
                        requestId))
                {
                    document.Dispose();
                    continue;
                }

                ThrowIfError(document, method, failureCategory, failureFieldPath);
                return document;
            }
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or EndOfStreamException or IOException)
        {
            throw CreateProtocolException(
                failureCategory,
                failureFieldPath,
                method,
                exception);
        }
        finally
        {
            requestGate.Release();
        }
    }

    public async Task SendNotificationAsync(
        string method,
        object? parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        try
        {
            var process = session.RequireRunningProcess(
                failureCategory,
                failureFieldPath);
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method
            };
            if (parameters is not null)
            {
                payload["params"] = JsonSerializer.SerializeToNode(
                    parameters,
                    SerializerOptions);
            }

            await McpJsonRpcFraming.WriteMessageAsync(
                process.StandardInput.BaseStream,
                payload,
                descriptor.MessageFraming,
                cancellationToken);
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw CreateProtocolException(
                failureCategory,
                failureFieldPath,
                method,
                exception);
        }
        finally
        {
            requestGate.Release();
        }
    }

    private static void ThrowIfError(
        JsonDocument document,
        string method,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        if (!document.RootElement.TryGetProperty("error", out var error))
        {
            return;
        }

        var detail = error.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString() ?? error.GetRawText()
            : error.GetRawText();
        document.Dispose();
        throw new McpSetupException(
            category,
            fieldPath,
            $"MCP method '{method}' failed. {detail}",
            $"Inspect the MCP server implementation for '{method}'.");
    }

    private McpSetupException CreateProtocolException(
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string method,
        Exception exception)
    {
        return new McpSetupException(
            category,
            fieldPath,
            $"MCP method '{method}' failed for '{descriptor.ServerKey}'. {exception.Message}{session.BuildStandardErrorSuffix()}",
            "Inspect the MCP process stderr and protocol framing.");
    }
}
