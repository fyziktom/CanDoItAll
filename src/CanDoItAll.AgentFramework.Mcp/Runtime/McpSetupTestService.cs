using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
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

        var validationFailure = ValidateDescriptor(descriptor, correlationId);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        IMcpRuntimeClient? client = null;
        var discoveredTools = Array.Empty<DiscoveredMcpTool>();
        try
        {
            client = await clientFactory.CreateAsync(descriptor, correlationId, cancellationToken);
            await client.StartAsync(cancellationToken);
            discoveredTools = (await client.ListToolsAsync(cancellationToken)).ToArray();
            var allowlistFailure = ValidateAllowedTools(descriptor, correlationId, discoveredTools);
            if (allowlistFailure is not null)
            {
                var cleanup = await TryCleanupAsync(client, descriptor, correlationId, CancellationToken.None);
                return WithCleanup(allowlistFailure, cleanup);
            }

            var successCleanup = await TryCleanupAsync(client, descriptor, correlationId, CancellationToken.None);
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

            return McpSetupTestResult.Success(descriptor, correlationId, discoveredTools, allowedTools, successCleanup.Completed);
        }
        catch (TimeoutException exception)
        {
            var cleanup = await TryCleanupAsync(client, descriptor, correlationId, CancellationToken.None);
            return WithCleanup(Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"MCP setup for '{descriptor.ServerKey}' timed out. {exception.Message}",
                "Increase timeout only after confirming the MCP server starts and responds predictably.",
                discoveredTools,
                cleanup.Completed), cleanup);
        }
        catch (OperationCanceledException)
        {
            var cleanup = await TryCleanupAsync(client, descriptor, correlationId, CancellationToken.None);
            return WithCleanup(Failure(
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
            var cleanup = await TryCleanupAsync(client, descriptor, correlationId, CancellationToken.None);
            return WithCleanup(Failure(
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

    private static McpSetupTestResult? ValidateDescriptor(
        McpServerDescriptor descriptor,
        string correlationId)
    {
        if (descriptor.AvailabilityState != CapabilityAvailabilityState.Available)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CapabilityUnavailable,
                "$.availabilityState",
                $"MCP server '{descriptor.ServerKey}' is {descriptor.AvailabilityState}.",
                "Enable or replace the MCP server before setup testing.");
        }

        if (descriptor.AllowedTools.Count == 0 && descriptor is LocalStdioMcpServerDescriptor)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.TemplateValidation,
                "$.allowedTools",
                $"Local MCP server '{descriptor.ServerKey}' must declare at least one allowed tool before launch.",
                "Run setup discovery or add explicit allowedTools before enabling local stdio MCP.");
        }

        if (descriptor is LocalStdioMcpServerDescriptor local)
        {
            if (local.RawEnvironmentVariables.Count > 0)
            {
                return Failure(
                    descriptor,
                    correlationId,
                    CapabilityDiagnosticCategory.SecretBinding,
                    "$.environmentVariables",
                    $"Local MCP server '{descriptor.ServerKey}' persists raw environment variables.",
                    "Replace raw environment variables with environmentVariableBindings.");
            }

            if (!LocalMcpCommandPolicy.IsAllowed(local.Command))
            {
                return Failure(
                    descriptor,
                    correlationId,
                    CapabilityDiagnosticCategory.CommandPolicy,
                    "$.command",
                    $"Local MCP command '{local.Command}' is outside the approved command policy.",
                    $"Use an approved command. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
            }
        }

        if (descriptor is RemoteHttpMcpServerDescriptor remote && remote.RawHeaders.Count > 0)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SecretBinding,
                "$.headers",
                $"Remote MCP server '{descriptor.ServerKey}' persists raw headers.",
                "Replace raw headers with headerBindings.");
        }

        return null;
    }

    private static McpSetupTestResult? ValidateAllowedTools(
        McpServerDescriptor descriptor,
        string correlationId,
        IReadOnlyList<DiscoveredMcpTool> discoveredTools)
    {
        var discoveredToolNames = discoveredTools
            .Select(tool => tool.Name)
            .ToHashSet();
        var missingTools = descriptor.AllowedTools
            .Where(tool => !discoveredToolNames.Contains(tool))
            .ToArray();

        if (missingTools.Length == 0)
        {
            return null;
        }

        return Failure(
            descriptor,
            correlationId,
            CapabilityDiagnosticCategory.McpListTools,
            "$.allowedTools",
            $"MCP server '{descriptor.ServerKey}' did not expose allowed tool(s): {string.Join(", ", missingTools.Select(tool => tool.Value))}.",
            "Update allowedTools to match discovered tools or repair the MCP server list-tools response.",
            discoveredTools);
    }

    private static async Task<CleanupAttempt> TryCleanupAsync(
        IMcpRuntimeClient? client,
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            return new(false, null);
        }

        try
        {
            await client.StopAsync(cancellationToken);
            return new(true, null);
        }
        catch (Exception exception)
        {
            return new(false, McpDiagnostics.Create(
                CapabilityDiagnosticCategory.ResourceCleanup,
                descriptor,
                "$.cleanup",
                $"MCP cleanup for '{descriptor.ServerKey}' failed. {exception.Message}",
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

    private static McpSetupTestResult Failure(
        McpServerDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint,
        IReadOnlyList<DiscoveredMcpTool>? discoveredTools = null,
        bool cleanupCompleted = false,
        int? httpStatusCode = null)
    {
        return McpSetupTestResult.Failure(
            descriptor,
            correlationId,
            [
                McpDiagnostics.Create(
                    category,
                    descriptor,
                    fieldPath,
                    detail,
                    repairHint,
                    correlationId,
                    httpStatusCode)
            ],
            discoveredTools,
            cleanupCompleted);
    }

    private sealed record CleanupAttempt(
        bool Completed,
        CapabilityDiagnostic? Diagnostic);
}
