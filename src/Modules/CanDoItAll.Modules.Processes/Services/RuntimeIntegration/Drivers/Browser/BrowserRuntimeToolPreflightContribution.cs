using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;

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
