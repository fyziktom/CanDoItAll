using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed class McpSetupTestService(IMcpClientFactory clientFactory) : IMcpSetupTestService
{
    public async Task<McpSetupTestResult> TestAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var validationFailure = McpSetupValidator.ValidateDescriptor(
            descriptor,
            correlationId);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        IMcpRuntimeClient? client = null;
        var discoveredTools = Array.Empty<DiscoveredMcpTool>();
        try
        {
            client = await clientFactory.CreateAsync(
                descriptor,
                correlationId,
                cancellationToken);
            await client.StartAsync(cancellationToken);
            discoveredTools = (await client.ListToolsAsync(cancellationToken)).ToArray();
            var allowlistFailure = McpSetupValidator.ValidateAllowedTools(
                descriptor,
                correlationId,
                discoveredTools);
            if (allowlistFailure is not null)
            {
                var cleanup = await TryCleanupAsync(
                    client,
                    descriptor,
                    correlationId,
                    CancellationToken.None);
                return WithCleanup(allowlistFailure, cleanup);
            }

            var successCleanup = await TryCleanupAsync(
                client,
                descriptor,
                correlationId,
                CancellationToken.None);
            var allowedTools = discoveredTools
                .Where(tool => descriptor.AllowedTools.Contains(tool.Name))
                .ToArray();
            if (successCleanup.Diagnostic is not null)
            {
                return McpSetupTestResult.Failure(
                    descriptor,
                    correlationId,
                    [successCleanup.Diagnostic],
                    discoveredTools,
                    cleanupCompleted: false);
            }

            return McpSetupTestResult.Success(
                descriptor,
                correlationId,
                discoveredTools,
                allowedTools,
                successCleanup.Completed);
        }
        catch (TimeoutException exception)
        {
            var cleanup = await TryCleanupAsync(
                client,
                descriptor,
                correlationId,
                CancellationToken.None);
            return WithCleanup(McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"MCP setup for '{descriptor.ServerKey}' timed out ({exception.GetType().Name}).",
                "Increase timeout only after confirming the MCP server starts and responds predictably.",
                discoveredTools,
                cleanup.Completed), cleanup);
        }
        catch (OperationCanceledException)
        {
            var cleanup = await TryCleanupAsync(
                client,
                descriptor,
                correlationId,
                CancellationToken.None);
            return WithCleanup(McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.Cancellation,
                "$",
                $"MCP setup for '{descriptor.ServerKey}' was cancelled.",
                "Retry only if the caller still owns the setup lifecycle.",
                discoveredTools,
                cleanup.Completed), cleanup);
        }
        catch (McpSetupException exception)
        {
            var cleanup = await TryCleanupAsync(
                client,
                descriptor,
                correlationId,
                CancellationToken.None);
            return WithCleanup(McpSetupFailureFactory.Create(
                descriptor,
                correlationId,
                exception.Category,
                exception.FieldPath,
                exception.Detail,
                exception.RepairHint,
                discoveredTools,
                cleanup.Completed,
                exception.HttpStatusCode), cleanup);
        }
    }

    private static async Task<CleanupAttempt> TryCleanupAsync(
        IMcpRuntimeClient? client,
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            return new CleanupAttempt(false, null);
        }

        try
        {
            await client.StopAsync(cancellationToken);
            return new CleanupAttempt(true, null);
        }
        catch (Exception exception)
        {
            return new CleanupAttempt(false, McpDiagnostics.Create(
                CapabilityDiagnosticCategory.ResourceCleanup,
                descriptor,
                "$.cleanup",
                $"MCP cleanup for '{descriptor.ServerKey}' failed ({exception.GetType().Name}).",
                "Inspect the MCP server shutdown path before enabling this descriptor.",
                correlationId));
        }
    }

    private static McpSetupTestResult WithCleanup(
        McpSetupTestResult result,
        CleanupAttempt cleanup)
    {
        if (cleanup.Diagnostic is null)
        {
            return result with { CleanupCompleted = cleanup.Completed };
        }

        return result with
        {
            CleanupCompleted = false,
            Diagnostics = [..result.Diagnostics, cleanup.Diagnostic]
        };
    }

    private sealed record CleanupAttempt(
        bool Completed,
        CapabilityDiagnostic? Diagnostic);
}
