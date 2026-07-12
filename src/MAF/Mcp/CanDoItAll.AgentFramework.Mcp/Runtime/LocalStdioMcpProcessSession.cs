using System.Diagnostics;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpProcessSession(
    LocalStdioMcpServerDescriptor descriptor)
{
    private readonly McpStandardErrorCollector standardError = new();
    private Process? process;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (process is not null)
        {
            throw new InvalidOperationException("MCP process has already been started.");
        }

        process = await LocalStdioMcpProcessLauncher.StartAsync(
            descriptor,
            cancellationToken);
        standardError.Start(process.StandardError);
    }

    public Process RequireRunningProcess(
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
            throw ExitedProcessException(currentProcess, category, fieldPath);
        }

        return currentProcess;
    }

    public async Task<string> ReadNextMessageAsync(
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
            throw ExitedProcessException(currentProcess, category, fieldPath);
        }
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
                await WaitForExitAsync(currentProcess, cancellationToken);
            }

            await standardError.WaitForCompletionAsync();
        }
        finally
        {
            currentProcess.Dispose();
        }
    }

    public string BuildStandardErrorSuffix()
    {
        return standardError.BuildDiagnosticSuffix();
    }

    private async Task WaitForExitAsync(
        Process currentProcess,
        CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
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

    private McpSetupException ExitedProcessException(
        Process currentProcess,
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        return new McpSetupException(
            category,
            fieldPath,
            $"MCP server '{descriptor.ServerKey}' exited with code {currentProcess.ExitCode}.{BuildStandardErrorSuffix()}",
            "Inspect the MCP command, arguments, working directory, and stderr output.");
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
