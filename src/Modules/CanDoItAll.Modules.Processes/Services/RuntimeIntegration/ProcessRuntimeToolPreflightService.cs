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

    public IReadOnlyList<ProcessRuntimeToolPlanGuardIssue> PlanIssues { get; init; } = [];

    public IReadOnlyList<AgentCapabilityDiagnostic> CapabilityDiagnostics { get; init; } = [];
}

internal sealed record ProcessRuntimeToolPlanGuardEvaluation(
    string PolicyName,
    IReadOnlyList<ProcessRuntimeToolPlanGuardIssue> Issues)
{
    public bool IsSatisfied => Issues.Count == 0;
}

internal interface IProcessRuntimeToolPlanGuard
{
    ProcessRuntimeToolPlanGuardEvaluation Evaluate(ProcessRuntimeStepAssignment assignment);
}

internal sealed class ProcessRuntimeToolPreflightService : IProcessRuntimeToolPreflightService
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

    private readonly IReadOnlyList<IAgentRuntimeToolProvider> runtimeToolProviders;
    private readonly IReadOnlyList<IProcessRuntimeToolPlanGuard> toolPlanGuards;
    private readonly ProcessRuntimeToolPreflightContributionCatalog toolPreflightContributions;

    public ProcessRuntimeToolPreflightService(
        IEnumerable<IAgentRuntimeToolProvider> runtimeToolProviders,
        IEnumerable<IProcessRuntimeToolPlanGuard>? toolPlanGuards,
        ProcessRuntimeToolPreflightContributionCatalog toolPreflightContributions)
    {
        ArgumentNullException.ThrowIfNull(runtimeToolProviders);
        ArgumentNullException.ThrowIfNull(toolPreflightContributions);

        this.runtimeToolProviders = runtimeToolProviders
            .OrderBy(provider => provider.Order)
            .ToArray();
        this.toolPlanGuards = (toolPlanGuards ?? []).ToArray();
        this.toolPreflightContributions = toolPreflightContributions;
    }

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
        var toolPlanGuardEvaluation = EvaluateToolPlanGuards(request.Assignment);
        if (!toolPlanGuardEvaluation.IsSatisfied)
        {
            return CreateToolPlanFailure(toolPlanGuardEvaluation);
        }

        if (requiredToolNames.Length == 0)
        {
            return ProcessRuntimeToolPreflightResult.Satisfied;
        }

        var contributionContext = new ProcessRuntimeToolPreflightContributionContext(
            request,
            requiredToolNames,
            ProcessRuntimeProviderContextFactory.Create(request.Assignment));
        toolPreflightContributions.Contribute(contributionContext);
        var capabilityDiagnostics = contributionContext.CapabilityDiagnostics
            .Concat(EvaluateRequiredRuntimeToolCapabilities(
                request,
                requiredToolNames,
                contributionContext.HandledToolNames))
            .DistinctBy(diagnostic => (diagnostic.Kind, diagnostic.CapabilityKey))
            .ToArray();
        if (capabilityDiagnostics.Length > 0)
        {
            return CreateCapabilityFailure(capabilityDiagnostics);
        }

        var contextIntent = contributionContext.ContextIntent;
        var context = CreateProviderContext(request.Agent, contextIntent);
        var composedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddConfiguredWorkspaceToolNames(composedToolNames, request.Agent, contextIntent);
        composedToolNames.UnionWith(contributionContext.ComposedToolNames);
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

    private ProcessRuntimeToolPlanGuardEvaluation EvaluateToolPlanGuards(
        ProcessRuntimeStepAssignment assignment)
    {
        var evaluations = toolPlanGuards
            .Select(guard => guard.Evaluate(assignment))
            .Where(evaluation => !evaluation.IsSatisfied)
            .ToArray();
        return evaluations.Length == 0
            ? new ProcessRuntimeToolPlanGuardEvaluation(string.Empty, [])
            : new ProcessRuntimeToolPlanGuardEvaluation(
                string.Join(", ", evaluations.Select(evaluation => evaluation.PolicyName)),
                evaluations.SelectMany(evaluation => evaluation.Issues).ToArray());
    }

    private static ProcessRuntimeToolPreflightResult CreateToolPlanFailure(
        ProcessRuntimeToolPlanGuardEvaluation guardResult)
    {
        var summary = string.Join(" ", guardResult.Issues.Select(issue => issue.SafeSummary));
        return new ProcessRuntimeToolPreflightResult(
            false,
            [],
            $"Deterministic tool-plan guard '{guardResult.PolicyName}' failed. {summary}")
        {
            PlanIssues = guardResult.Issues
        };
    }

    private static ProcessRuntimeToolPreflightResult CreateCapabilityFailure(
        IReadOnlyList<AgentCapabilityDiagnostic> diagnostics)
    {
        var missingCapabilities = diagnostics
            .Select(diagnostic => $"{diagnostic.Kind}:{diagnostic.CapabilityKey}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessRuntimeToolPreflightResult(
            false,
            [],
            $"Required runtime tool capability assignment(s) are missing for this process step: {string.Join(", ", missingCapabilities)}.")
        {
            CapabilityDiagnostics = diagnostics
        };
    }

    private static IReadOnlyList<AgentCapabilityDiagnostic> EvaluateRequiredRuntimeToolCapabilities(
        ProcessRuntimeToolPreflightRequest request,
        IReadOnlyList<string> requiredToolNames,
        IReadOnlySet<string> contributionHandledToolNames)
    {
        var diagnostics = new List<AgentCapabilityDiagnostic>();
        foreach (var requiredToolName in requiredToolNames)
        {
            var normalizedToolName = ToolContractCatalog.NormalizeToolName(requiredToolName);
            if (string.IsNullOrWhiteSpace(normalizedToolName))
            {
                continue;
            }

            if (contributionHandledToolNames.Contains(normalizedToolName))
            {
                continue;
            }

            if (!normalizedToolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) ||
                !AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(normalizedToolName, out _))
            {
                continue;
            }

            AddMissingWorkspaceToolCapabilityDiagnostic(request, normalizedToolName, diagnostics);
        }

        return diagnostics
            .DistinctBy(diagnostic => (diagnostic.Kind, diagnostic.CapabilityKey))
            .ToArray();
    }

    private static void AddMissingWorkspaceToolCapabilityDiagnostic(
        ProcessRuntimeToolPreflightRequest request,
        string normalizedToolName,
        List<AgentCapabilityDiagnostic> diagnostics)
    {
        var capabilityKey = normalizedToolName.Replace('_', '-');
        if (request.Agent.Capabilities.Any(capability =>
                capability.Kind == CapabilityKind.Tool &&
                CapabilityKeyMatchesTool(capability.CapabilityKey, normalizedToolName, capabilityKey)))
        {
            return;
        }

        diagnostics.Add(CreateMissingCapabilityDiagnostic(
            request,
            CapabilityKind.Tool,
            capabilityKey,
            $"Step '{request.Assignment.StepKey}' requires runtime tool '{normalizedToolName}', but agent '{request.Agent.Name}' does not have matching tool capability '{capabilityKey}'."));
    }

    private static AgentCapabilityDiagnostic CreateMissingCapabilityDiagnostic(
        ProcessRuntimeToolPreflightRequest request,
        CapabilityKind kind,
        string capabilityKey,
        string message)
    {
        return new AgentCapabilityDiagnostic(
            AgentCapabilityDiagnosticCode.MissingRequiredCapability,
            AgentCapabilityDiagnosticSeverity.Error,
            request.Agent.Id,
            request.Agent.Name,
            string.IsNullOrWhiteSpace(request.Assignment.RoleKey)
                ? request.Assignment.StepKey
                : request.Assignment.RoleKey,
            request.Agent.RoleTitle,
            kind,
            capabilityKey,
            message);
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
