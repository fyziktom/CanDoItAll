using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal static class LocalStdioMcpProcessLauncher
{
    public static async Task<IWorkspaceDuplexProcessSession> StartAsync(
        LocalStdioMcpServerDescriptor descriptor,
        string correlationId,
        IWorkspaceLongRunningProcessHost processHost,
        IWorkspacePathResolutionService pathResolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(processHost);
        ArgumentNullException.ThrowIfNull(pathResolver);

        string workingDirectory;
        try
        {
            workingDirectory = pathResolver.ResolveDirectoryPath(
                descriptor.WorkingDirectory,
                allowMissing: false,
                descriptor.AllowedWorkingDirectories.ToArray()).FullPath;
        }
        catch (UnauthorizedAccessException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.PermissionDenied,
                "$.workingDirectory",
                $"MCP server '{descriptor.ServerKey}' cannot access its configured working directory.",
                "Grant the application identity access or select another authorized working directory.");
        }
        catch (Exception exception) when (
            exception is WorkspacePathResolutionException or
                DirectoryNotFoundException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.WorkingDirectory,
                "$.workingDirectory",
                $"MCP server '{descriptor.ServerKey}' does not have an accessible working directory.",
                "Select an existing authorized workspace or external-target directory.");
        }

        PlaywrightMcpLaunchResolution launch;
        try
        {
            launch = await PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workingDirectory,
                    descriptor.Command,
                    descriptor.Arguments,
                    processHost,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? new PlaywrightMcpLaunchResolution(
                    descriptor.Command,
                    descriptor.Arguments);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PlatformNotSupportedException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.UnsupportedPlatform,
                "$.command",
                $"MCP server '{descriptor.ServerKey}' requires a runtime that is unsupported on this host.",
                "Install a supported shell-neutral Node/npm runtime or disable this local MCP capability.");
        }
        catch (WorkspaceExecutableResolutionException exception)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.RuntimeDependency,
                "$.command",
                $"MCP server '{descriptor.ServerKey}' runtime resolution failed ({exception.Failure}).",
                "Install the approved Node/npm runtime on PATH or disable this local MCP capability.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.PermissionDenied,
                "$.arguments",
                $"MCP server '{descriptor.ServerKey}' cannot access its managed package root.",
                "Grant the application identity access to the managed tool root or select another workspace.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                TimeoutException or
                IOException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.PackageSetup,
                "$.arguments",
                $"MCP server '{descriptor.ServerKey}' package setup failed ({exception.GetType().Name}).",
                "Verify the pinned package version, managed tool-root permissions, and npm connectivity.");
        }

        string executablePath;
        try
        {
            executablePath = new WorkspaceExecutableLocator().ResolveExecutablePath(
                [launch.Command],
                workingDirectory);
        }
        catch (WorkspaceExecutableResolutionException exception)
        {
            throw new McpSetupException(
                exception.Failure == WorkspaceExecutableResolutionFailure.ForeignPathSyntax
                    ? CapabilityDiagnosticCategory.CommandPolicy
                    : CapabilityDiagnosticCategory.RuntimeDependency,
                "$.command",
                $"MCP server '{descriptor.ServerKey}' executable resolution failed ({exception.Failure}).",
                "Install the approved runtime on PATH or select an approved executable for this host.");
        }

        if (!LocalMcpCommandPolicy.IsResolvedExecutableAllowed(executablePath))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.CommandPolicy,
                "$.command",
                $"MCP server '{descriptor.ServerKey}' resolved to an executable outside its approved command policy.",
                $"Use an approved command. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
        }

        IReadOnlyDictionary<string, string?> environmentVariables;
        try
        {
            environmentVariables = LocalStdioMcpEnvironmentBinder.Build(descriptor);
        }
        catch (McpSetupException)
        {
            throw;
        }

        IWorkspaceProcessSession session;
        try
        {
            session = await processHost.StartSessionAsync(
                    new WorkspaceProcessSessionRequest(
                        ToolName: "local_stdio_mcp",
                        RecipeId: correlationId,
                        ExecutablePath: executablePath,
                        Arguments: launch.Arguments,
                        WorkingDirectory: workingDirectory,
                        EnvironmentVariables: environmentVariables,
                        StdoutLimitCharacters: 256,
                        StderrLimitCharacters: 16 * 1024,
                        TerminationMode: WorkspaceProcessTerminationMode.GracefulThenForceTree,
                        StandardIoMode: WorkspaceProcessStandardIoMode.Duplex,
                        StderrCaptureMode: WorkspaceProcessTextCaptureMode.Tail),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WorkspaceProcessStartException)
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.ProcessStart,
                "$.command",
                $"MCP server '{descriptor.ServerKey}' could not start its approved executable.",
                "Verify executable permissions, runtime dependencies, and the authorized working directory.");
        }

        if (session is IWorkspaceDuplexProcessSession duplexSession)
        {
            return duplexSession;
        }

        await session.DisposeAsync().ConfigureAwait(false);
        throw new McpSetupException(
            CapabilityDiagnosticCategory.RuntimeAdapter,
            "$.transport",
            $"MCP server '{descriptor.ServerKey}' requires a duplex-capable process host.",
            "Configure the canonical workspace process host with duplex stdio support.");
    }
}
