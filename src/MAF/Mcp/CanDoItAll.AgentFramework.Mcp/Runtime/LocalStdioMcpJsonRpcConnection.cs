using System.Globalization;
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
    private const string JsonRpcVersion = "2.0";
    private const int MaximumUnmatchedMessages = 64;
    private const int MaximumMessageIdCharacters = 128;
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web)
    {
        MaxDepth = 64
    };

    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly SemaphoreSlim writerGate = new(1, 1);
    private int nextRequestId;

    public async Task<JsonDocument> SendRequestAsync(
        string method,
        object parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var processSession = session.RequireRunningSession(
                failureCategory,
                failureFieldPath);
            var requestId = Interlocked.Increment(ref nextRequestId);
            var payload = new JsonObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(
                    parameters,
                    SerializerOptions)
            };
            await WriteMessageAsync(
                processSession.StandardInput,
                payload,
                cancellationToken);

            var peerRequestIds = new HashSet<string>(StringComparer.Ordinal);
            var unmatchedResponseIds = new HashSet<string>(StringComparer.Ordinal);
            var unmatchedMessageCount = 0;
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
                var root = document.RootElement;
                ValidateEnvelope(
                    root,
                    method,
                    failureCategory,
                    failureFieldPath);

                if (root.TryGetProperty("method", out _))
                {
                    IncrementUnmatchedCount(
                        ref unmatchedMessageCount,
                        method,
                        failureCategory,
                        failureFieldPath);
                    try
                    {
                        await HandlePeerMessageAsync(
                            processSession.StandardInput,
                            root,
                            peerRequestIds,
                            method,
                            failureCategory,
                            failureFieldPath,
                            cancellationToken);
                    }
                    finally
                    {
                        document.Dispose();
                    }

                    continue;
                }

                if (!root.TryGetProperty("id", out var id))
                {
                    document.Dispose();
                    throw ProtocolFailure(
                        failureCategory,
                        failureFieldPath,
                        method,
                        McpTransportFailureKind.InvalidMessage,
                        "A JSON-RPC message without a method must contain a response ID.");
                }

                var responseIdKey = GetMessageIdKey(
                    id,
                    method,
                    failureCategory,
                    failureFieldPath);
                if (!LocalStdioMcpResponseParser.IsResponseForRequest(root, requestId))
                {
                    if (!unmatchedResponseIds.Add(responseIdKey))
                    {
                        document.Dispose();
                        throw ProtocolFailure(
                            failureCategory,
                            failureFieldPath,
                            method,
                            McpTransportFailureKind.DuplicateMessageId,
                            "The peer repeated an unmatched JSON-RPC response ID.");
                    }

                    document.Dispose();
                    IncrementUnmatchedCount(
                        ref unmatchedMessageCount,
                        method,
                        failureCategory,
                        failureFieldPath);
                    continue;
                }

                ThrowIfError(
                    document,
                    method,
                    failureCategory,
                    failureFieldPath);
                if (!root.TryGetProperty("result", out _))
                {
                    document.Dispose();
                    throw new McpSetupException(
                        failureCategory,
                        failureFieldPath,
                        $"MCP method '{method}' returned neither a result nor an error.",
                        $"Repair the MCP server response for '{method}'.",
                        transportFailure: McpTransportFailureKind.InvalidMessage);
                }

                return document;
            }
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or EndOfStreamException or InvalidDataException or IOException)
        {
            throw CreateProtocolException(
                failureCategory,
                failureFieldPath,
                method,
                exception);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task SendNotificationAsync(
        string method,
        object? parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken);
        try
        {
            var processSession = session.RequireRunningSession(
                failureCategory,
                failureFieldPath);
            var payload = new JsonObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["method"] = method
            };
            if (parameters is not null)
            {
                payload["params"] = JsonSerializer.SerializeToNode(
                    parameters,
                    SerializerOptions);
            }

            await WriteMessageAsync(
                processSession.StandardInput,
                payload,
                cancellationToken);
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or IOException)
        {
            throw CreateProtocolException(
                failureCategory,
                failureFieldPath,
                method,
                exception);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task HandlePeerMessageAsync(
        Stream standardInput,
        JsonElement root,
        HashSet<string> peerRequestIds,
        string awaitedMethod,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(methodElement.GetString()))
        {
            throw ProtocolFailure(
                category,
                fieldPath,
                awaitedMethod,
                McpTransportFailureKind.InvalidMessage,
                "The peer sent a JSON-RPC request with an invalid method.");
        }

        if (!root.TryGetProperty("id", out var id))
        {
            return;
        }

        var idKey = GetMessageIdKey(
            id,
            awaitedMethod,
            category,
            fieldPath);
        if (!peerRequestIds.Add(idKey))
        {
            throw ProtocolFailure(
                category,
                fieldPath,
                awaitedMethod,
                McpTransportFailureKind.DuplicateMessageId,
                "The peer repeated a JSON-RPC request ID.");
        }

        var method = methodElement.GetString();
        var response = new JsonObject
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["id"] = JsonNode.Parse(id.GetRawText())
        };
        if (string.Equals(method, "ping", StringComparison.Ordinal))
        {
            response["result"] = new JsonObject();
        }
        else
        {
            response["error"] = new JsonObject
            {
                ["code"] = -32601,
                ["message"] = "Method not found"
            };
        }

        await WriteMessageAsync(standardInput, response, cancellationToken);
    }

    private async Task WriteMessageAsync(
        Stream standardInput,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        await writerGate.WaitAsync(cancellationToken);
        try
        {
            await McpJsonRpcFraming.WriteMessageAsync(
                standardInput,
                payload,
                messageFraming,
                cancellationToken);
        }
        finally
        {
            writerGate.Release();
        }
    }

    private static void ValidateEnvelope(
        JsonElement root,
        string method,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw ProtocolFailure(
                category,
                fieldPath,
                method,
                McpTransportFailureKind.InvalidMessage,
                "The peer returned a non-object JSON-RPC message.");
        }

        var idCount = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("id"))
            {
                idCount++;
            }
        }

        if (idCount > 1)
        {
            throw ProtocolFailure(
                category,
                fieldPath,
                method,
                McpTransportFailureKind.DuplicateMessageId,
                "The peer sent a JSON-RPC message with duplicate ID properties.");
        }

        if (!LocalStdioMcpResponseParser.HasSupportedJsonRpcVersion(root))
        {
            throw ProtocolFailure(
                category,
                fieldPath,
                method,
                McpTransportFailureKind.InvalidMessage,
                "The peer returned an invalid JSON-RPC protocol version.");
        }
    }

    private static string GetMessageIdKey(
        JsonElement id,
        string method,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var numericId))
        {
            return "n:" + numericId.ToString(CultureInfo.InvariantCulture);
        }

        if (id.ValueKind == JsonValueKind.String &&
            id.GetString() is { Length: > 0 and <= MaximumMessageIdCharacters } stringId)
        {
            return "s:" + stringId;
        }

        throw ProtocolFailure(
            category,
            fieldPath,
            method,
            McpTransportFailureKind.InvalidMessageId,
            "The peer sent an invalid JSON-RPC message ID.");
    }

    private static void IncrementUnmatchedCount(
        ref int unmatchedMessageCount,
        string method,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        unmatchedMessageCount++;
        if (unmatchedMessageCount <= MaximumUnmatchedMessages)
        {
            return;
        }

        throw ProtocolFailure(
            category,
            fieldPath,
            method,
            McpTransportFailureKind.ExcessiveUnmatchedMessages,
            $"The peer exceeded the limit of {MaximumUnmatchedMessages} unmatched JSON-RPC messages.");
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

    private static McpSetupException ProtocolFailure(
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string method,
        McpTransportFailureKind failure,
        string detail)
    {
        return new McpSetupException(
            category,
            fieldPath,
            $"MCP method '{method}' failed protocol validation ({failure}). {detail}",
            "Repair the MCP server JSON-RPC control flow and framing.",
            transportFailure: failure);
    }

    private McpSetupException CreateProtocolException(
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string method,
        Exception exception)
    {
        var failure = exception switch
        {
            McpMessageTooLargeException => McpTransportFailureKind.MessageTooLarge,
            EndOfStreamException => McpTransportFailureKind.EndOfStream,
            JsonException => McpTransportFailureKind.InvalidJson,
            InvalidDataException => McpTransportFailureKind.InvalidMessage,
            IOException => McpTransportFailureKind.IoFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(exception))
        };
        return new McpSetupException(
            category,
            fieldPath,
            $"MCP method '{method}' failed for '{serverKey}' ({failure}).{session.BuildStandardErrorSuffix()}",
            "Inspect the MCP process stderr and protocol framing.",
            transportFailure: failure);
    }
}
