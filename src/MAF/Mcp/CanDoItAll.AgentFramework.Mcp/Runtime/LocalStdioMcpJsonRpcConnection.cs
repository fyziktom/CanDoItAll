using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpJsonRpcConnection(
    McpServerKey serverKey,
    McpStdioMessageFraming messageFraming,
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
            var processSession = session.RequireRunningSession(
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
                processSession.StandardInput,
                payload,
                messageFraming,
                cancellationToken);

            while (true)
            {
                var message = await session.ReadNextMessageAsync(
                    processSession,
                    messageFraming,
                    failureCategory,
                    failureFieldPath,
                    cancellationToken);
                var document = LocalStdioMcpResponseParser.ParseMessage(
                    message,
                    serverKey,
                    failureCategory,
                    failureFieldPath);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    document.Dispose();
                    throw new McpSetupException(
                        failureCategory,
                        failureFieldPath,
                        $"MCP method '{method}' returned a non-object JSON-RPC response.",
                        "Repair the MCP server JSON-RPC response shape.");
                }

                if (!LocalStdioMcpResponseParser.HasSupportedJsonRpcVersion(
                        document.RootElement))
                {
                    document.Dispose();
                    throw new McpSetupException(
                        failureCategory,
                        failureFieldPath,
                        $"MCP method '{method}' returned an invalid JSON-RPC protocol version.",
                        "Repair the MCP server JSON-RPC response to use version '2.0'.");
                }

                if (!LocalStdioMcpResponseParser.IsResponseForRequest(
                        document.RootElement,
                        requestId))
                {
                    document.Dispose();
                    continue;
                }

                ThrowIfError(document, method, failureCategory, failureFieldPath);
                if (!document.RootElement.TryGetProperty("result", out _))
                {
                    document.Dispose();
                    throw new McpSetupException(
                        failureCategory,
                        failureFieldPath,
                        $"MCP method '{method}' returned neither a result nor an error.",
                        $"Repair the MCP server response for '{method}'.");
                }

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
            var processSession = session.RequireRunningSession(
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
                processSession.StandardInput,
                payload,
                messageFraming,
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
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("error", out var error))
        {
            return;
        }

        var code = error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("code", out var codeElement) &&
            codeElement.ValueKind == JsonValueKind.Number &&
            codeElement.TryGetInt32(out var numericCode)
                ? $" Error code: {numericCode}."
                : string.Empty;
        document.Dispose();
        throw new McpSetupException(
            category,
            fieldPath,
            $"MCP method '{method}' failed.{code}",
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
            $"MCP method '{method}' failed for '{serverKey}'. {exception.GetType().Name}.{session.BuildStandardErrorSuffix()}",
            "Inspect the MCP process stderr and protocol framing.");
    }
}
