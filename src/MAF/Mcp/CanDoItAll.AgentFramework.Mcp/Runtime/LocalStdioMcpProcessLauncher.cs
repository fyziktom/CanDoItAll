using System.Diagnostics;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpProcessLauncher
{
    public static async Task<Process> StartAsync(
        LocalStdioMcpServerDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workingDirectory = McpExecutableResolver.ResolveWorkingDirectory(
            descriptor.WorkingDirectory);
        var launch = await ResolveLaunchAsync(
                descriptor,
                workingDirectory,
                cancellationToken)
            .ConfigureAwait(false);
        var startInfo = CreateStartInfo(launch, workingDirectory);
        LocalStdioMcpEnvironmentBinder.Apply(startInfo, descriptor);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        try
        {
            if (!process.Start())
            {
                throw new McpSetupException(
                    CapabilityDiagnosticCategory.ProcessStart,
                    "$.command",
                    $"MCP command '{descriptor.Command}' did not start a process.",
                    "Check the MCP command and arguments.");
            }

            return process;
        }
        catch (McpSetupException)
        {
            process.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            process.Dispose();
            throw new McpSetupException(
                CapabilityDiagnosticCategory.ProcessStart,
                "$.command",
                $"MCP command '{descriptor.Command}' failed to start. {exception.Message}",
                "Check that the command exists on PATH and the working directory is valid.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        PlaywrightMcpLaunchResolution launch,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = McpExecutableResolver.ResolveExecutablePath(launch.Command),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in launch.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task<PlaywrightMcpLaunchResolution> ResolveLaunchAsync(
        LocalStdioMcpServerDescriptor descriptor,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var resolution = await PlaywrightMcpLaunchResolver.TryResolveAsync(
                workingDirectory,
                descriptor.Command,
                descriptor.Arguments,
                cancellationToken)
            .ConfigureAwait(false);
        return resolution ?? new PlaywrightMcpLaunchResolution(
            descriptor.Command,
            descriptor.Arguments);
    }
}
