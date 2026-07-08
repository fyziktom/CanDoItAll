using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRuntimeToolPreflightService
{
    ValueTask<ProcessRuntimeToolPreflightResult> EvaluateAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken);
}

internal sealed record ProcessRuntimeToolPreflightRequest(
    ProcessRuntimeStepAssignment Assignment,
    AgentDefinition Agent,
    IReadOnlyList<string> RequiredRuntimeToolNames);

internal sealed record ProcessRuntimeToolPreflightResult(
    bool IsSatisfied,
    IReadOnlyList<string> MissingToolNames,
    string Summary)
{
    public static ProcessRuntimeToolPreflightResult Satisfied { get; } = new(true, [], string.Empty);
}

internal sealed class ProcessRuntimeToolPreflightService(
    IEnumerable<IAgentRuntimeToolProvider> runtimeToolProviders) : IProcessRuntimeToolPreflightService
{
    private static readonly ProviderProfile PreflightProvider = new(
        Guid.Empty,
        "Runtime tool preflight",
        ProviderKind.OpenAi,
        BaseUrl: string.Empty,
        ApiKeyEnvironmentVariable: string.Empty,
        DefaultModel: "preflight",
        ProviderTransportKind.Responses,
        IsEnabled: true,
        SupportsStreaming: false,
        SupportsTools: true,
        PreferFrameworkManagedChatHistory: true,
        SupportsBackgroundResponses: false,
        ConfigurationJson: "{}",
        Notes: string.Empty,
        HealthStatus: "PreflightOnly",
        LastCheckedAtUtc: null,
        SuggestedModels: []);

    private readonly IReadOnlyList<IAgentRuntimeToolProvider> runtimeToolProviders = runtimeToolProviders
        .OrderBy(provider => provider.Order)
        .ToArray();

    public async ValueTask<ProcessRuntimeToolPreflightResult> EvaluateAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requiredToolNames = request.RequiredRuntimeToolNames
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Select(toolName => toolName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requiredToolNames.Length == 0)
        {
            return ProcessRuntimeToolPreflightResult.Satisfied;
        }

        var contextIntent = CreateContextIntent(request.Assignment);
        var context = CreateProviderContext(request.Agent, contextIntent);
        var composedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddConfiguredWorkspaceToolNames(composedToolNames, request.Agent, contextIntent);
        AddAssignedBrowserToolNames(composedToolNames, request.Agent, contextIntent, requiredToolNames);
        var providerErrors = new List<string>();
        foreach (var provider in runtimeToolProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var tools = await provider.CreateToolsAsync(context, cancellationToken).ConfigureAwait(false);
                foreach (var tool in tools)
                {
                    if (!string.IsNullOrWhiteSpace(tool.Name))
                    {
                        composedToolNames.Add(tool.Name.Trim());
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var providerKey = provider.Descriptor?.ProviderKey ?? provider.GetType().Name;
                providerErrors.Add($"{providerKey}: {exception.Message}");
            }
        }

        var missingToolNames = requiredToolNames
            .Where(toolName => !composedToolNames.Contains(toolName))
            .ToArray();
        if (missingToolNames.Length == 0)
        {
            return ProcessRuntimeToolPreflightResult.Satisfied;
        }

        var providerErrorSummary = providerErrors.Count == 0
            ? string.Empty
            : $" Provider errors: {string.Join("; ", providerErrors)}.";
        return new ProcessRuntimeToolPreflightResult(
            false,
            missingToolNames,
            $"Required runtime tool(s) are not composed for this process step: {string.Join(", ", missingToolNames)}.{providerErrorSummary}");
    }

    private static AgentRuntimeContextIntent CreateContextIntent(
        ProcessRuntimeStepAssignment assignment)
    {
        return new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: assignment.StepKey,
            ProcessRunId: assignment.RunId.Value.ToString("D"),
            ProcessStepId: assignment.StepInstanceId.Value.ToString("D"),
            TargetScope: assignment.OperationTargetScope,
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: assignment.AllowedOperations.Any(operation =>
                string.Equals(operation, ProcessOperationContractNames.CaptureRuntimeProof, StringComparison.OrdinalIgnoreCase)),
            ScaffoldToolOnly: false,
            AllowsProductMutation: assignment.AllowedOperations.Any(operation =>
                string.Equals(operation, ProcessOperationContractNames.MutateProductTarget, StringComparison.OrdinalIgnoreCase)),
            WorkspaceToolProfile: null,
            WorkspaceScope: null,
            AllowedOperations: assignment.AllowedOperations,
            RuntimeToolProvidersEnabled: true,
            WorkspaceToolsEnabled: true,
            CapabilityScopeOverride: AgentFrameworkProcessCapabilityScopeTranslator.Translate(assignment.CapabilityScope));
    }

    private static AgentRuntimeToolProviderContext CreateProviderContext(
        AgentDefinition agent,
        AgentRuntimeContextIntent contextIntent)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            PreflightProvider,
            Capabilities: [],
            SuppressApprovalRequirements: true,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            RuntimeSessionKey: string.Empty,
            contextIntent,
            Tags: new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static void AddConfiguredWorkspaceToolNames(
        HashSet<string> composedToolNames,
        AgentDefinition agent,
        AgentRuntimeContextIntent contextIntent)
    {
        if (!agent.Permissions.CanUseTools ||
            !contextIntent.WorkspaceToolsEnabled ||
            !RuntimeToolProcessIntentPolicy.ShouldExposeConfiguredWorkspaceToolsForProcessIntent(contextIntent))
        {
            return;
        }

        var workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson);
        if (!CanAttachConfiguredWorkspaceTools(workspaceToolAccess))
        {
            return;
        }

        foreach (var toolName in ToolContractCatalog.WorkspaceToolNames)
        {
            var normalizedToolName = ToolContractCatalog.NormalizeToolName(toolName);
            if (!AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(normalizedToolName, out _) ||
                !AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(workspaceToolAccess, normalizedToolName) ||
                !ToolCapabilityRegistry.TryResolve(normalizedToolName, out var capability) ||
                !RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(capability, contextIntent))
            {
                continue;
            }

            composedToolNames.Add(normalizedToolName);
        }
    }

    private static void AddAssignedBrowserToolNames(
        HashSet<string> composedToolNames,
        AgentDefinition agent,
        AgentRuntimeContextIntent contextIntent,
        IReadOnlyList<string> requiredToolNames)
    {
        if (!agent.Permissions.CanUseTools ||
            !contextIntent.BrowserToolsAllowed)
        {
            return;
        }

        foreach (var requiredToolName in requiredToolNames.Where(IsBrowserRuntimeToolName))
        {
            var normalizedToolName = ToolContractCatalog.NormalizeToolName(requiredToolName);
            if (HasRequiredBrowserRuntimeToolCapability(agent, normalizedToolName) &&
                ToolCapabilityRegistry.TryResolve(normalizedToolName, out var capability) &&
                RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(capability, contextIntent))
            {
                composedToolNames.Add(normalizedToolName);
            }
        }
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
        => toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

    private static bool CanAttachConfiguredWorkspaceTools(
        AgentWorkspaceToolAccessSettings workspaceToolAccess)
    {
        return workspaceToolAccess.AllowedExternalTargetAliases.Count > 0 ||
               workspaceToolAccess.CanWriteFiles ||
               workspaceToolAccess.CanRunValidationCommands ||
               workspaceToolAccess.CanRunLocalScripts ||
               workspaceToolAccess.CanScaffoldProjects ||
               workspaceToolAccess.CanManageWorkspacePaths ||
               workspaceToolAccess.CanTransformArtifacts;
    }
}
