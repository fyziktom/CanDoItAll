using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserRuntimeToolPreflightContribution : IProcessRuntimeToolPreflightContribution
{
    public string ContributionKey => "browser.runtime-tool-preflight";

    public int Order => 100;

    public void Contribute(ProcessRuntimeToolPreflightContributionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var browserToolNames = context.RequiredToolNames
            .Where(IsBrowserRuntimeToolName)
            .Select(ToolContractCatalog.NormalizeToolName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var browserToolName in browserToolNames)
        {
            context.MarkToolHandled(browserToolName);
        }

        if (browserToolNames.Length == 0 ||
            !BrowserRuntimeToolAccessPolicy.AllowsBrowserTools(context.Request.Assignment))
        {
            return;
        }

        context.ReplaceContextIntent(context.ContextIntent with { BrowserToolsAllowed = true });
        foreach (var browserToolName in browserToolNames)
        {
            if (!HasRequiredBrowserRuntimeToolCapability(context.Request.Agent, browserToolName))
            {
                context.AddCapabilityDiagnostic(CreateMissingCapabilityDiagnostic(context, browserToolName));
                continue;
            }

            if (context.Request.Agent.Permissions.CanUseTools &&
                ToolCapabilityRegistry.TryResolve(browserToolName, out var capability) &&
                RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(
                    capability,
                    context.ContextIntent))
            {
                context.AddComposedToolName(browserToolName);
            }
        }
    }

    private static AgentCapabilityDiagnostic CreateMissingCapabilityDiagnostic(
        ProcessRuntimeToolPreflightContributionContext context,
        string normalizedToolName)
    {
        var request = context.Request;
        return new AgentCapabilityDiagnostic(
            AgentCapabilityDiagnosticCode.MissingRequiredCapability,
            AgentCapabilityDiagnosticSeverity.Error,
            request.Agent.Id,
            request.Agent.Name,
            string.IsNullOrWhiteSpace(request.Assignment.RoleKey)
                ? request.Assignment.StepKey
                : request.Assignment.RoleKey,
            request.Agent.RoleTitle,
            CapabilityKind.McpServer,
            "playwright-local-mcp",
            $"Step '{request.Assignment.StepKey}' requires browser runtime tool '{normalizedToolName}', but agent '{request.Agent.Name}' does not have a Playwright/browser MCP capability or matching browser tool capability.");
    }

    private static bool HasRequiredBrowserRuntimeToolCapability(
        AgentDefinition agent,
        string requiredToolName)
    {
        var normalizedToolName = requiredToolName.Trim().Replace('-', '_');
        var normalizedToolKey = normalizedToolName.Replace('_', '-');
        return agent.Capabilities.Any(capability =>
            capability.Kind switch
            {
                CapabilityKind.McpServer => IsBrowserMcpServerCapability(capability.CapabilityKey),
                CapabilityKind.Tool => CapabilityKeyMatchesTool(capability.CapabilityKey, normalizedToolName, normalizedToolKey),
                _ => false
            });
    }

    private static bool IsBrowserMcpServerCapability(string capabilityKey)
    {
        return capabilityKey.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
               capabilityKey.Contains("browser-mcp", StringComparison.OrdinalIgnoreCase) ||
               capabilityKey.Contains("browser_mcp", StringComparison.OrdinalIgnoreCase);
    }

    internal static BrowserMcpTransportRequirement ResolveMcpTransportRequirement(
        AgentDefinition agent,
        IReadOnlyList<string> requiredToolNames,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(requiredToolNames);
        ArgumentNullException.ThrowIfNull(capabilityCatalog);
        if (!requiredToolNames.Any(IsBrowserRuntimeToolName))
        {
            return BrowserMcpTransportRequirement.NotApplicable;
        }

        var assignments = agent.Capabilities
            .Where(capability =>
                capability.Kind == CapabilityKind.McpServer &&
                IsBrowserMcpServerCapability(capability.CapabilityKey))
            .ToArray();
        if (assignments.Length == 0)
        {
            return BrowserMcpTransportRequirement.NotApplicable;
        }

        var transports = assignments
            .Select(assignment => capabilityCatalog
                .Where(capability =>
                    capability.Id == assignment.CapabilityId &&
                    capability.Kind == CapabilityKind.McpServer)
                .Take(2)
                .ToArray())
            .Select(matches => matches.Length == 1
                ? ResolveTransport(matches[0])
                : BrowserMcpTransportRequirement.Invalid)
            .ToArray();
        if (transports.Any(transport => transport == BrowserMcpTransportRequirement.Invalid))
        {
            return BrowserMcpTransportRequirement.Invalid;
        }

        var distinctTransports = transports.Distinct().ToArray();
        if (distinctTransports.Length != 1)
        {
            return BrowserMcpTransportRequirement.Invalid;
        }

        return distinctTransports[0];
    }

    internal static bool RequiresCapabilityCatalog(
        AgentDefinition agent,
        IReadOnlyList<string> requiredToolNames)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(requiredToolNames);
        return requiredToolNames.Any(IsBrowserRuntimeToolName) &&
               agent.Capabilities.Any(capability => capability.Kind == CapabilityKind.McpServer);
    }

    private static BrowserMcpTransportRequirement ResolveTransport(CapabilityCatalogItem? capability)
    {
        if (capability is null)
        {
            return BrowserMcpTransportRequirement.Invalid;
        }

        try
        {
            using var document = JsonDocument.Parse(capability.ConfigurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return BrowserMcpTransportRequirement.Invalid;
            }

            var root = document.RootElement;
            if (root.TryGetProperty("hosted", out var hosted) &&
                hosted.ValueKind == JsonValueKind.True)
            {
                return BrowserMcpTransportRequirement.Remote;
            }

            var transport = root.TryGetProperty("transport", out var transportElement) &&
                transportElement.ValueKind == JsonValueKind.String
                    ? transportElement.GetString()
                    : null;
            if (string.Equals(transport, "stdio", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("command", out var commandElement) &&
                    commandElement.ValueKind != JsonValueKind.String)
                {
                    return BrowserMcpTransportRequirement.Invalid;
                }

                var command = commandElement.ValueKind == JsonValueKind.String
                    ? commandElement.GetString()
                    : null;
                return !string.IsNullOrWhiteSpace(command) &&
                       new WorkspaceExecutableAuthorizationPolicy()
                           .IsAllowedCommandName(command, ["npx"])
                    ? BrowserMcpTransportRequirement.LocalStdioNode
                    : BrowserMcpTransportRequirement.LocalStdio;
            }

            if (string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(transport, "logical", StringComparison.OrdinalIgnoreCase))
            {
                return BrowserMcpTransportRequirement.Remote;
            }

            return BrowserMcpTransportRequirement.Invalid;
        }
        catch (JsonException)
        {
            return BrowserMcpTransportRequirement.Invalid;
        }
    }

    private static bool CapabilityKeyMatchesTool(
        string capabilityKey,
        string normalizedToolName,
        string normalizedToolKey)
    {
        var keyWithUnderscores = capabilityKey.Replace('-', '_');
        return string.Equals(capabilityKey, normalizedToolKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(keyWithUnderscores, normalizedToolName, StringComparison.OrdinalIgnoreCase) ||
               keyWithUnderscores.EndsWith($"_{normalizedToolName}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserRuntimeToolName(string toolName)
    {
        return toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);
    }
}

internal enum BrowserMcpTransportRequirement
{
    NotApplicable,
    LocalStdio,
    LocalStdioNode,
    Remote,
    Invalid
}
