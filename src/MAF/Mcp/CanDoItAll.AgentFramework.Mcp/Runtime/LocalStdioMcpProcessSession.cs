using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpProcessSession
{
    private readonly McpServerKey serverKey;
    private readonly string correlationId;
    private readonly IWorkspaceLongRunningProcessHost processHost;
    private readonly IWorkspacePathResolutionService pathResolver;
    private LocalStdioMcpServerDescriptor? pendingDescriptor;
    private IWorkspaceDuplexProcessSession? session;

    public LocalStdioMcpProcessSession(
        LocalStdioMcpServerDescriptor descriptor,
        string correlationId,
        IWorkspaceLongRunningProcessHost processHost,
        IWorkspacePathResolutionService pathResolver)
    {
        pendingDescriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        serverKey = descriptor.ServerKey;
        this.correlationId = correlationId;
        this.processHost = processHost ?? throw new ArgumentNullException(nameof(processHost));
        this.pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (session is not null)
        {
            throw new InvalidOperationException("MCP process has already been started.");
        }

        var descriptor = Interlocked.Exchange(ref pendingDescriptor, null)
            ?? throw new InvalidOperationException("MCP process launch has already been attempted.");
        session = await LocalStdioMcpProcessLauncher.StartAsync(
                descriptor,
                correlationId,
                processHost,
                pathResolver,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public IWorkspaceDuplexProcessSession RequireRunningSession(
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        var currentSession = session;
        if (currentSession is null)
        {
            throw new McpSetupException(
                category,
                fieldPath,
                $"MCP server '{serverKey}' has not been started.",
                "Start the MCP runtime before calling MCP methods.");
        }

        if (currentSession.HasExited)
        {
            throw ExitedProcessException(category, fieldPath);
        }

        return currentSession;
    }

    public async Task<string> ReadNextMessageAsync(
        IWorkspaceDuplexProcessSession currentSession,
        McpStdioMessageFraming messageFraming,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await McpJsonRpcFraming.ReadMessageAsync(
                currentSession.StandardOutput,
                messageFraming,
                cancellationToken);
        }
        catch (EndOfStreamException) when (currentSession.HasExited)
        {
            throw ExitedProcessException(category, fieldPath);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var currentSession = Interlocked.Exchange(ref session, null);
        pendingDescriptor = null;
        if (currentSession is null)
        {
            return;
        }

        try
        {
            currentSession.CompleteStandardInput();
            using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            grace.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                var result = await currentSession.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                EnsureCleanupConfirmed(result.ResidualProcessPossible);
            }
            catch (OperationCanceledException)
            {
                var result = await currentSession.TerminateAsync(
                    WorkspaceProcessTerminationReason.CallerCanceled,
                    "The MCP process did not exit during bounded shutdown.",
                    CancellationToken.None).ConfigureAwait(false);
                EnsureCleanupConfirmed(result.ResidualProcessPossible);
            }
        }
        finally
        {
            await currentSession.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void EnsureCleanupConfirmed(bool residualProcessPossible)
    {
        if (!residualProcessPossible)
        {
            return;
        }

        throw new McpSetupException(
            CapabilityDiagnosticCategory.ResourceCleanup,
            "$.cleanup",
            $"MCP server '{serverKey}' process-tree cleanup could not be confirmed.",
            "Inspect and reclaim the owned MCP process tree before retrying this server.");
    }

    public string BuildStandardErrorSuffix()
    {
        var hasStandardError = !string.IsNullOrWhiteSpace(session?.CaptureOutput().Stderr);
        return !hasStandardError
            ? string.Empty
            : " Bounded stderr was captured and withheld from diagnostics.";
    }

    private McpSetupException ExitedProcessException(
        CapabilityDiagnosticCategory category,
        string fieldPath)
    {
        return new McpSetupException(
            category,
            fieldPath,
            $"MCP server '{serverKey}' exited unexpectedly.{BuildStandardErrorSuffix()}",
            "Inspect the MCP command, managed package, and bounded stderr output.");
    }
}
