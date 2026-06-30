using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public sealed class ExternalProcessToolInvoker(
    IExternalProcessRunner? processRunner = null) : IExternalProcessToolInvoker
{
    private readonly IExternalProcessRunner processRunner = processRunner ?? new LocalExternalProcessRunner();

    public async Task<ToolInvocationResult> InvokeAsync(
        ExternalProcessToolDescriptor descriptor,
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCommandAllowed(descriptor))
        {
            return ToolInvocationResult.Failure(request.CorrelationId,
            [
                ToolDiagnostics.Create(
                    CapabilityDiagnosticCategory.CommandPolicy,
                    descriptor,
                    "$.executablePath",
                    $"Executable '{descriptor.ExecutablePath}' is not allowed for capability '{descriptor.Identity.Key}'.",
                    "Use an allowed executable declared by the capability template command policy.",
                    request.CorrelationId,
                    CapabilityTransportKind.ExternalProcess)
            ]);
        }

        ExternalProcessRunResult runResult;
        try
        {
            runResult = await processRunner.RunAsync(
                new ExternalProcessRunRequest(
                    descriptor.ExecutablePath,
                    descriptor.Arguments,
                    descriptor.WorkingDirectory,
                    descriptor.Timeout,
                    request.Input.GetRawText(),
                    descriptor.MaxOutputBytes,
                    request.CorrelationId),
                cancellationToken);
        }
        catch (TimeoutException exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"Process invocation for '{descriptor.ExecutablePath}' exceeded timeout {descriptor.Timeout}. {exception.Message}",
                "Increase the timeout only after confirming the external tool is healthy and bounded.",
                timeout: descriptor.Timeout);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Cancellation,
                "$",
                $"Process invocation for '{descriptor.ExecutablePath}' was cancelled.",
                "Retry only if the caller still owns the setup or tool-call lifecycle.",
                timeout: descriptor.Timeout);
        }
        catch (Exception exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessStart,
                "$.executablePath",
                $"Failed to start '{descriptor.ExecutablePath}'. {exception.GetType().Name}: {exception.Message}",
                "Check the executable path, working directory, and command policy.");
        }

        if (!runResult.Started)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessStart,
                "$.executablePath",
                $"Process '{descriptor.ExecutablePath}' did not start. stderr: {runResult.Stderr}",
                "Check the executable path and command policy.");
        }

        if (runResult.ExitCode != 0)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessExit,
                "$.exitCode",
                $"Process '{descriptor.ExecutablePath}' exited with {runResult.ExitCode}. stdout: {runResult.Stdout}; stderr: {runResult.Stderr}",
                "Inspect the non-zero exit output and repair the external tool command or setup payload.",
                exitCode: runResult.ExitCode);
        }

        return ParseAndValidateOutput(
            descriptor,
            request.CorrelationId,
            runResult.Stdout,
            CapabilityTransportKind.ExternalProcess);
    }

    private static bool IsCommandAllowed(ExternalProcessToolDescriptor descriptor)
    {
        var executableName = Path.GetFileName(descriptor.ExecutablePath);
        return descriptor.AllowedExecutableNames.Contains(executableName);
    }

    private static ToolInvocationResult ParseAndValidateOutput(
        ExternalProcessToolDescriptor descriptor,
        string correlationId,
        string output,
        CapabilityTransportKind transport)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(output);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.JsonParse,
                "$",
                $"Output from '{descriptor.ExecutablePath}' was not valid JSON. {exception.Message}. Output: {output}",
                "Return a JSON object matching the external tool output schema.");
        }

        foreach (var property in descriptor.RequiredOutputProperties)
        {
            if (root.TryGetProperty(property, out _))
            {
                continue;
            }

            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SchemaValidation,
                "$." + property,
                $"Output from '{descriptor.ExecutablePath}' did not include required property '{property}'. Output: {output}",
                "Return all required output schema properties from the external tool.",
                transport: transport);
        }

        return ToolInvocationResult.Success(correlationId, root);
    }

    private static ToolInvocationResult Failure(
        ExternalProcessToolDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint,
        int? exitCode = null,
        TimeSpan? timeout = null,
        CapabilityTransportKind transport = CapabilityTransportKind.ExternalProcess)
    {
        return ToolInvocationResult.Failure(correlationId,
        [
            ToolDiagnostics.Create(
                category,
                descriptor,
                fieldPath,
                detail,
                repairHint,
                correlationId,
                transport,
                exitCode: exitCode,
                timeout: timeout)
        ]);
    }
}

internal sealed class LocalExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessRunResult> RunAsync(
        ExternalProcessRunRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(request.Timeout);
        }

        var executionToken = timeoutSource.Token;
        process.StartInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            var started = process.Start();
            if (!started)
            {
                return new ExternalProcessRunResult(false, -1, string.Empty, string.Empty, stopwatch.Elapsed);
            }

            await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), executionToken);
            process.StandardInput.Close();
            var stdoutTask = ReadBoundedAsync(process.StandardOutput, request.MaxOutputBytes, executionToken);
            var stderrTask = ReadBoundedAsync(process.StandardError, request.MaxOutputBytes, executionToken);
            await process.WaitForExitAsync(executionToken);
            stopwatch.Stop();

            return new ExternalProcessRunResult(
                true,
                process.ExitCode,
                await stdoutTask,
                await stderrTask,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Process exceeded the configured timeout of {request.Timeout}.");
        }
    }

    private static async Task<string> ReadBoundedAsync(
        TextReader reader,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        maxCharacters = Math.Max(0, maxCharacters);
        var buffer = new char[1024];
        var builder = new StringBuilder(capacity: Math.Min(maxCharacters, 4096));
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            var remaining = maxCharacters - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(read, remaining));
            }
        }

        return builder.ToString();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
