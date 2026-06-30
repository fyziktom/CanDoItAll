using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed class LocalStdioMcpClientFactory : IMcpClientFactory
{
    public Task<IMcpRuntimeClient> CreateAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        return descriptor switch
        {
            LocalStdioMcpServerDescriptor local => Task.FromResult<IMcpRuntimeClient>(new LocalStdioMcpRuntimeClient(local)),
            RemoteHttpMcpServerDescriptor => throw new McpSetupException(
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.transport",
                $"Remote HTTP MCP setup testing is not implemented for '{descriptor.ServerKey}'.",
                "Use local stdio MCP setup testing or add a remote MCP HTTP client implementation."),
            InternalHostedMcpServerDescriptor => throw new McpSetupException(
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.transport",
                $"Internal hosted MCP setup testing is not implemented for '{descriptor.ServerKey}'.",
                "Use local stdio MCP setup testing or add an internal hosted MCP client implementation."),
            _ => throw new McpSetupException(
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.transport",
                $"MCP descriptor kind '{descriptor.DescriptorKind}' is not supported by this client factory.",
                "Use a supported MCP descriptor kind before running setup tests.")
        };
    }
}

internal sealed class LocalStdioMcpRuntimeClient(
    LocalStdioMcpServerDescriptor descriptor) : IMcpRuntimeClient
{
    private const string ProtocolVersion = "2025-06-18";
    private const int MaximumDiagnosticCharacters = 8192;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim requestGate = new(1, 1);
    private readonly StringBuilder standardError = new();
    private readonly object standardErrorGate = new();
    private int nextRequestId;
    private Process? process;
    private Task? standardErrorPump;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return RunWithTimeoutAsync(
            async operationToken =>
            {
                StartProcess(operationToken);
                await SendRequestAsync(
                    "initialize",
                    new
                    {
                        protocolVersion = ProtocolVersion,
                        capabilities = new { },
                        clientInfo = new
                        {
                            name = "CanDoItAll",
                            version = typeof(LocalStdioMcpClientFactory).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                        }
                    },
                    CapabilityDiagnosticCategory.McpHandshake,
                    "$.initialize",
                    operationToken);
                await SendNotificationAsync(
                    "notifications/initialized",
                    parameters: null,
                    CapabilityDiagnosticCategory.McpHandshake,
                    "$.initialized",
                    operationToken);
            },
            "MCP initialize handshake",
            cancellationToken);
    }

    public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        return RunWithTimeoutAsync(
            async operationToken =>
            {
                using var response = await SendRequestAsync(
                    "tools/list",
                    new { },
                    CapabilityDiagnosticCategory.McpListTools,
                    "$.tools",
                    operationToken);
                return ParseListToolsResponse(response);
            },
            "MCP tools/list request",
            cancellationToken);
    }

    public Task<string> CallToolAsync(
        McpToolName toolName,
        string jsonArguments,
        CancellationToken cancellationToken)
    {
        return RunWithTimeoutAsync(
            async operationToken =>
            {
                var arguments = ParseToolArguments(jsonArguments);
                using var response = await SendRequestAsync(
                    "tools/call",
                    new
                    {
                        name = toolName.Value,
                        arguments
                    },
                    CapabilityDiagnosticCategory.McpListTools,
                    "$.tools.call",
                    operationToken);

                return response.RootElement.TryGetProperty("result", out var result)
                    ? result.GetRawText()
                    : response.RootElement.GetRawText();
            },
            $"MCP tools/call request for '{toolName.Value}'",
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var currentProcess = process;
        process = null;
        if (currentProcess is null)
        {
            return;
        }

        try
        {
            if (!currentProcess.HasExited)
            {
                TryCloseStandardInput(currentProcess);
                using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                waitCancellation.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await currentProcess.WaitForExitAsync(waitCancellation.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    currentProcess.Kill(entireProcessTree: true);
                    await currentProcess.WaitForExitAsync(CancellationToken.None);
                }
            }

            if (standardErrorPump is not null)
            {
                await Task.WhenAny(standardErrorPump, Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None));
            }
        }
        finally
        {
            currentProcess.Dispose();
        }
    }

    private void StartProcess(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (process is not null)
        {
            throw new InvalidOperationException("MCP process has already been started.");
        }

        var workingDirectory = ResolveWorkingDirectory(descriptor.WorkingDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveExecutablePath(descriptor.Command),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in descriptor.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        AddEnvironmentVariableBindings(startInfo);

        var startedProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!startedProcess.Start())
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.ProcessStart,
                    "$.command",
                    $"MCP command '{descriptor.Command}' did not start a process.",
                    "Check the MCP command and arguments.");
            }
        }
        catch (McpSetupException)
        {
            startedProcess.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            startedProcess.Dispose();
            throw new McpSetupException(
                CapabilityDiagnosticCategory.ProcessStart,
                "$.command",
                $"MCP command '{descriptor.Command}' failed to start. {exception.Message}",
                "Check that the command exists on PATH and the working directory is valid.");
        }

        process = startedProcess;
        standardErrorPump = PumpStandardErrorAsync(startedProcess.StandardError);
    }

    private static string ResolveWorkingDirectory(string workingDirectory)
    {
        var candidate = string.IsNullOrWhiteSpace(workingDirectory)
            ? "."
            : workingDirectory.Trim();
        return Path.GetFullPath(Path.IsPathRooted(candidate)
            ? candidate
            : Path.Combine(Environment.CurrentDirectory, candidate));
    }

    private static string ResolveExecutablePath(string command)
    {
        var trimmed = command.Trim();
        if (Path.IsPathRooted(trimmed) || trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            return trimmed;
        }

        foreach (var directory in EnumeratePathDirectories())
        {
            foreach (var candidate in EnumerateExecutableCandidates(trimmed))
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return trimmed;
    }

    private static IEnumerable<string> EnumeratePathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (var item in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = item.Trim();
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string command)
    {
        if (!OperatingSystem.IsWindows() || !string.IsNullOrWhiteSpace(Path.GetExtension(command)))
        {
            yield return command;
            yield break;
        }

        var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        foreach (var extension in pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return command + extension.Trim();
        }

        yield return command;
    }

    private void AddEnvironmentVariableBindings(ProcessStartInfo startInfo)
    {
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

    private async Task<JsonDocument> SendRequestAsync(
        string method,
        object parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        try
        {
            var currentProcess = RequireRunningProcess(failureCategory, failureFieldPath);
            var requestId = Interlocked.Increment(ref nextRequestId);
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = JsonSerializer.SerializeToNode(parameters, SerializerOptions)
            };

            await McpJsonRpcFraming.WriteMessageAsync(
                currentProcess.StandardInput.BaseStream,
                payload,
                descriptor.MessageFraming,
                cancellationToken);

            while (true)
            {
                var message = await ReadNextMessageAsync(currentProcess, failureCategory, failureFieldPath, cancellationToken);
                var document = ParseMessage(message, failureCategory, failureFieldPath);
                if (!IsResponseForRequest(document.RootElement, requestId))
                {
                    document.Dispose();
                    continue;
                }

                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    var detail = error.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? error.GetRawText()
                        : error.GetRawText();
                    document.Dispose();
                    throw new McpSetupException(
                        failureCategory,
                        failureFieldPath,
                        $"MCP method '{method}' failed. {detail}",
                        $"Inspect the MCP server implementation for '{method}'.");
                }

                return document;
            }
        }
        catch (McpSetupException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw CreateProtocolException(failureCategory, failureFieldPath, method, exception);
        }
        catch (EndOfStreamException exception)
        {
            throw CreateProtocolException(failureCategory, failureFieldPath, method, exception);
        }
        catch (IOException exception)
        {
            throw CreateProtocolException(failureCategory, failureFieldPath, method, exception);
        }
        finally
        {
            requestGate.Release();
        }
    }

    private async Task SendNotificationAsync(
        string method,
        object? parameters,
        CapabilityDiagnosticCategory failureCategory,
        string failureFieldPath,
        CancellationToken cancellationToken)
    {
        await requestGate.WaitAsync(cancellationToken);
        try
        {
            var currentProcess = RequireRunningProcess(failureCategory, failureFieldPath);
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method
            };
            if (parameters is not null)
            {
                payload["params"] = JsonSerializer.SerializeToNode(parameters, SerializerOptions);
            }

            await McpJsonRpcFraming.WriteMessageAsync(
                currentProcess.StandardInput.BaseStream,
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
            throw CreateProtocolException(failureCategory, failureFieldPath, method, exception);
        }
        finally
        {
            requestGate.Release();
        }
    }

    private Process RequireRunningProcess(
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        var currentProcess = process;
        if (currentProcess is null)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{descriptor.ServerKey}' has not been started.",
                "Start the MCP runtime before calling MCP methods.");
        }

        if (currentProcess.HasExited)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{descriptor.ServerKey}' exited with code {currentProcess.ExitCode}.{BuildStandardErrorSuffix()}",
                "Inspect the MCP command, arguments, working directory, and stderr output.");
        }

        return currentProcess;
    }

    private async Task<string> ReadNextMessageAsync(
        Process currentProcess,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await McpJsonRpcFraming.ReadMessageAsync(
                currentProcess.StandardOutput.BaseStream,
                descriptor.MessageFraming,
                cancellationToken);
        }
        catch (EndOfStreamException) when (currentProcess.HasExited)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{descriptor.ServerKey}' exited with code {currentProcess.ExitCode}.{BuildStandardErrorSuffix()}",
                "Inspect the MCP command, arguments, working directory, and stderr output.");
        }
    }

    private JsonDocument ParseMessage(
        string message,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        try
        {
            return JsonDocument.Parse(message);
        }
        catch (JsonException exception)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{descriptor.ServerKey}' returned invalid JSON. {exception.Message}",
                "Inspect the MCP server stdio framing and response payload.");
        }
    }

    private static bool IsResponseForRequest(JsonElement root, int requestId)
    {
        if (!root.TryGetProperty("id", out var id))
        {
            return false;
        }

        return id.ValueKind switch
        {
            JsonValueKind.Number => id.TryGetInt32(out var numericId) && numericId == requestId,
            JsonValueKind.String => string.Equals(id.GetString(), requestId.ToString(), StringComparison.Ordinal),
            _ => false
        };
    }

    private static IReadOnlyList<DiscoveredMcpTool> ParseListToolsResponse(JsonDocument response)
    {
        if (!response.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tools", out var tools) ||
            tools.ValueKind != JsonValueKind.Array)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.tools",
                "MCP tools/list response did not include a tools array.",
                "Repair the MCP server tools/list response.");
        }

        var discovered = new List<DiscoveredMcpTool>();
        foreach (var tool in tools.EnumerateArray())
        {
            if (!tool.TryGetProperty("name", out var nameElement) ||
                !McpToolName.TryCreate(nameElement.GetString(), out var name))
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.McpListTools,
                    "$.tools[].name",
                    "MCP tools/list response included an invalid tool name.",
                    "Repair the MCP server tools/list response to return valid MCP tool names.");
            }

            var description = tool.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? string.Empty
                : string.Empty;
            discovered.Add(new DiscoveredMcpTool(name, description));
        }

        return discovered;
    }

    private static object ParseToolArguments(string jsonArguments)
    {
        if (string.IsNullOrWhiteSpace(jsonArguments))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(jsonArguments) ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.TemplateValidation,
                "$.arguments",
                $"MCP tool arguments are not valid JSON. {exception.Message}",
                "Pass a JSON object as MCP tool arguments.");
        }
    }

    private async Task PumpStandardErrorAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        while (true)
        {
            var read = await reader.ReadAsync(buffer);
            if (read == 0)
            {
                return;
            }

            lock (standardErrorGate)
            {
                standardError.Append(buffer, 0, read);
                if (standardError.Length > MaximumDiagnosticCharacters)
                {
                    standardError.Remove(0, standardError.Length - MaximumDiagnosticCharacters);
                }
            }
        }
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
            $"MCP method '{method}' failed for '{descriptor.ServerKey}'. {exception.Message}{BuildStandardErrorSuffix()}",
            "Inspect the MCP process stderr and protocol framing.");
    }

    private string BuildStandardErrorSuffix()
    {
        lock (standardErrorGate)
        {
            if (standardError.Length == 0)
            {
                return string.Empty;
            }

            return $" Stderr: {standardError}";
        }
    }

    private async Task RunWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(descriptor.Timeout);
        try
        {
            await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{operationName} exceeded {descriptor.Timeout}.");
        }
    }

    private async Task<T> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(descriptor.Timeout);
        try
        {
            return await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{operationName} exceeded {descriptor.Timeout}.");
        }
    }

    private static void TryCloseStandardInput(Process currentProcess)
    {
        try
        {
            currentProcess.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
        }
        catch (IOException)
        {
        }
    }
}

internal static class McpJsonRpcFraming
{
    private const int MaximumNewlineDelimitedMessageBytes = 8 * 1024 * 1024;
    private static readonly Encoding HeaderEncoding = Encoding.ASCII;
    private static readonly Encoding BodyEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();
    private static readonly byte[] NewlineTerminator = "\n"u8.ToArray();

    public static async Task WriteMessageAsync(
        Stream stream,
        JsonObject payload,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        switch (messageFraming)
        {
            case McpStdioMessageFraming.ContentLength:
                await WriteContentLengthMessageAsync(stream, body, cancellationToken);
                return;
            case McpStdioMessageFraming.NewlineDelimitedJson:
                await WriteNewlineDelimitedMessageAsync(stream, body, cancellationToken);
                return;
            default:
                throw new InvalidDataException($"Unsupported MCP stdio message framing '{messageFraming}'.");
        }
    }

    public static Task WriteMessageAsync(
        Stream stream,
        JsonObject payload,
        CancellationToken cancellationToken)
        => WriteMessageAsync(stream, payload, McpStdioMessageFraming.ContentLength, cancellationToken);

    public static async Task<string> ReadMessageAsync(
        Stream stream,
        McpStdioMessageFraming messageFraming,
        CancellationToken cancellationToken)
    {
        return messageFraming switch
        {
            McpStdioMessageFraming.ContentLength => await ReadContentLengthMessageAsync(stream, cancellationToken),
            McpStdioMessageFraming.NewlineDelimitedJson => await ReadNewlineDelimitedMessageAsync(stream, cancellationToken),
            _ => throw new InvalidDataException($"Unsupported MCP stdio message framing '{messageFraming}'.")
        };
    }

    public static Task<string> ReadMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
        => ReadMessageAsync(stream, McpStdioMessageFraming.ContentLength, cancellationToken);

    private static async Task WriteContentLengthMessageAsync(
        Stream stream,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var header = HeaderEncoding.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WriteNewlineDelimitedMessageAsync(
        Stream stream,
        byte[] body,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(body, cancellationToken);
        await stream.WriteAsync(NewlineTerminator, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadContentLengthMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(stream, cancellationToken);
        var contentLength = ParseContentLength(header);
        var body = new byte[contentLength];
        var totalRead = 0;
        while (totalRead < body.Length)
        {
            var read = await stream.ReadAsync(body.AsMemory(totalRead, body.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP stdio stream ended while reading a message body.");
            }

            totalRead += read;
        }

        return BodyEncoding.GetString(body);
    }

    private static async Task<string> ReadNewlineDelimitedMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var body = await ReadNewlineDelimitedLineAsync(stream, cancellationToken);
            if (body.Length == 0)
            {
                continue;
            }

            return BodyEncoding.GetString(body);
        }
    }

    private static async Task<byte[]> ReadNewlineDelimitedLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var body = new List<byte>(512);
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                if (body.Count == 0)
                {
                    throw new EndOfStreamException("MCP stdio stream ended while reading a newline-delimited message.");
                }

                return body.ToArray();
            }

            var value = buffer[0];
            if (value == (byte)'\n')
            {
                if (body.Count > 0 && body[^1] == (byte)'\r')
                {
                    body.RemoveAt(body.Count - 1);
                }

                return body.ToArray();
            }

            body.Add(value);
            if (body.Count > MaximumNewlineDelimitedMessageBytes)
            {
                throw new InvalidDataException($"MCP newline-delimited message exceeded {MaximumNewlineDelimitedMessageBytes} bytes.");
            }
        }
    }

    private static async Task<string> ReadHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(128);
        var buffer = new byte[1];
        while (!EndsWith(headerBytes, HeaderTerminator))
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP stdio stream ended while reading message headers.");
            }

            headerBytes.Add(buffer[0]);
            if (headerBytes.Count > 8192)
            {
                throw new InvalidDataException("MCP stdio message header exceeded 8192 bytes.");
            }
        }

        return HeaderEncoding.GetString(headerBytes.ToArray());
    }

    private static int ParseContentLength(string header)
    {
        foreach (var line in header.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', count: 2);
            if (parts.Length == 2 &&
                string.Equals(parts[0].Trim(), "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[1].Trim(), out var contentLength) &&
                contentLength >= 0)
            {
                return contentLength;
            }
        }

        throw new InvalidDataException("MCP stdio message header is missing a valid Content-Length value.");
    }

    private static bool EndsWith(List<byte> source, byte[] suffix)
    {
        if (source.Count < suffix.Length)
        {
            return false;
        }

        for (var index = 0; index < suffix.Length; index++)
        {
            if (source[source.Count - suffix.Length + index] != suffix[index])
            {
                return false;
            }
        }

        return true;
    }
}
