using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRuntimeToolPreflightService
{
    ValueTask<ProcessRuntimeToolPreflightResult> EvaluateAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken);

    ValueTask<ProcessRuntimeToolPreflightResult> EvaluateHostCapabilitiesAsync(
        IReadOnlyList<string> requiredRuntimeToolNames,
        CancellationToken cancellationToken);

    ValueTask<ProcessRuntimeToolPreflightResult> EvaluateRequiredHostCapabilitiesAsync(
        IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
        CancellationToken cancellationToken);

    ValueTask<ProcessRuntimeToolPreflightResult> EvaluateStepHostCapabilitiesAsync(
        IReadOnlyList<string> declaredRuntimeToolNames,
        IReadOnlyList<string> effectiveRuntimeToolNames,
        IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
        CancellationToken cancellationToken);
}

internal sealed record ProcessRuntimeToolPreflightRequest(
    ProcessRuntimeStepAssignment Assignment,
    AgentDefinition Agent,
    IReadOnlyList<string> RequiredRuntimeToolNames,
    IReadOnlyList<CapabilityCatalogItem>? CapabilityCatalog = null,
    Func<CancellationToken, ValueTask<IReadOnlyList<CapabilityCatalogItem>>>? CapabilityCatalogResolver = null);

internal sealed record ProcessRuntimeToolPreflightResult(
    bool IsSatisfied,
    IReadOnlyList<string> MissingToolNames,
    string Summary)
{
    public static ProcessRuntimeToolPreflightResult Satisfied { get; } = new(true, [], string.Empty);

    public IReadOnlyList<ProcessRuntimeToolPlanGuardIssue> PlanIssues { get; init; } = [];

    public IReadOnlyList<AgentCapabilityDiagnostic> CapabilityDiagnostics { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeToolHostCapabilityFinding> HostCapabilityFindings { get; init; } = [];

    public ProcessHostCapabilityEvaluationEvidence? HostCapabilityEvidence { get; init; }
}

internal sealed record ProcessRuntimeToolHostCapabilityFinding(
    string RuntimeToolName,
    ProcessHostCapabilityId CapabilityId,
    ProcessHostCapabilityAvailability Availability,
    ProcessHostCapabilityReason? Reason,
    ProcessHostProfileId ProfileId);

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
    private const int MaximumRequiredRuntimeTools = 64;
    private const int MaximumRuntimeToolNameLength = 128;
    private const int MaximumRequiredHostCapabilities = 32;

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
    private readonly IProcessHostCapabilitySnapshotProvider? hostCapabilitySnapshotProvider;

    public ProcessRuntimeToolPreflightService(
        IEnumerable<IAgentRuntimeToolProvider> runtimeToolProviders,
        IEnumerable<IProcessRuntimeToolPlanGuard>? toolPlanGuards,
        ProcessRuntimeToolPreflightContributionCatalog toolPreflightContributions,
        IProcessHostCapabilitySnapshotProvider? hostCapabilitySnapshotProvider = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeToolProviders);
        ArgumentNullException.ThrowIfNull(toolPreflightContributions);

        this.runtimeToolProviders = runtimeToolProviders
            .OrderBy(provider => provider.Order)
            .ToArray();
        this.toolPlanGuards = (toolPlanGuards ?? []).ToArray();
        this.toolPreflightContributions = toolPreflightContributions;
        this.hostCapabilitySnapshotProvider = hostCapabilitySnapshotProvider;
    }

    public async ValueTask<ProcessRuntimeToolPreflightResult> EvaluateAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsBoundedRuntimeToolContract(request.RequiredRuntimeToolNames))
        {
            return InvalidRequirementContract("runtime-tool");
        }

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

        IReadOnlyCollection<ProcessHostCapabilityId> additionalHostCapabilities = [];
        if (BrowserRuntimeToolPreflightContribution.RequiresCapabilityCatalog(
                request.Agent,
                requiredToolNames))
        {
            request = await ResolveCapabilityCatalogAsync(request, cancellationToken).ConfigureAwait(false);
            var transportRequirement = BrowserRuntimeToolPreflightContribution.ResolveMcpTransportRequirement(
                request.Agent,
                requiredToolNames,
                request.CapabilityCatalog ?? []);
            if (transportRequirement == BrowserMcpTransportRequirement.Invalid)
            {
                return InvalidRequirementContract("browser-mcp-transport");
            }

            if (transportRequirement is BrowserMcpTransportRequirement.LocalStdio or
                BrowserMcpTransportRequirement.LocalStdioNode)
            {
                additionalHostCapabilities =
                    transportRequirement == BrowserMcpTransportRequirement.LocalStdioNode
                        ?
                        [
                            ProcessHostCapabilityIds.LocalStdioMcp,
                            ProcessHostCapabilityIds.NodeRuntime,
                            ProcessHostCapabilityIds.NodePackageManager
                        ]
                        : [ProcessHostCapabilityIds.LocalStdioMcp];
            }
        }

        var hostCapabilityResult = await EvaluateStepHostCapabilitiesAsync(
            requiredToolNames,
            requiredToolNames,
            additionalHostCapabilities,
            cancellationToken).ConfigureAwait(false);
        if (!hostCapabilityResult.IsSatisfied)
        {
            return hostCapabilityResult;
        }
        var hostCapabilityEvidence = hostCapabilityResult.HostCapabilityEvidence;

        var contributionContext = new ProcessRuntimeToolPreflightContributionContext(
            request,
            requiredToolNames,
            ProcessRuntimeProviderContextFactory.Create(request.Assignment));
        toolPreflightContributions.Contribute(contributionContext);
        if (RequiresCapabilityCatalogForDiagnostics(
                requiredToolNames,
                contributionContext.HandledToolNames))
        {
            request = await ResolveCapabilityCatalogAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var capabilityDiagnostics = contributionContext.CapabilityDiagnostics
            .Concat(EvaluateRequiredRuntimeToolCapabilities(
                request,
                requiredToolNames,
                contributionContext.HandledToolNames))
            .DistinctBy(diagnostic => (diagnostic.Kind, diagnostic.CapabilityKey))
            .ToArray();
        if (capabilityDiagnostics.Length > 0)
        {
            return CreateCapabilityFailure(capabilityDiagnostics) with
            {
                HostCapabilityEvidence = hostCapabilityEvidence
            };
        }

        var contextIntent = contributionContext.ContextIntent;
        var composedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddConfiguredWorkspaceToolNames(composedToolNames, request.Agent, contextIntent);
        composedToolNames.UnionWith(contributionContext.ComposedToolNames);
        if (requiredToolNames.All(composedToolNames.Contains))
        {
            return SatisfiedWithEvidence(hostCapabilityEvidence);
        }

        request = await ResolveCapabilityCatalogAsync(request, cancellationToken).ConfigureAwait(false);
        var context = CreateProviderContext(
            request.Agent,
            request.CapabilityCatalog ?? [],
            contextIntent);
        var providerFailureCount = 0;
        foreach (var provider in runtimeToolProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (provider.Descriptor is { } descriptor &&
                !descriptor.SupportedPurposes.Contains(context.Purpose))
            {
                continue;
            }

            try
            {
                var tools = await provider.CreateToolsAsync(context, cancellationToken).ConfigureAwait(false);
                foreach (var tool in tools)
                {
                    if (!string.IsNullOrWhiteSpace(tool.Name) &&
                        IsProviderToolAllowedForProcessIntent(tool.Name, contextIntent))
                    {
                        composedToolNames.Add(tool.Name.Trim());
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                providerFailureCount++;
            }
        }

        var missingToolNames = requiredToolNames
            .Where(toolName => !composedToolNames.Contains(toolName))
            .ToArray();
        if (missingToolNames.Length == 0)
        {
            return SatisfiedWithEvidence(hostCapabilityEvidence);
        }

        var providerErrorSummary = providerFailureCount == 0
            ? string.Empty
            : $" {providerFailureCount} runtime tool provider(s) failed during preflight; provider details were omitted from persisted diagnostics.";
        return new ProcessRuntimeToolPreflightResult(
            false,
            missingToolNames,
            $"Required runtime tool(s) are not composed for this process step: {string.Join(", ", missingToolNames)}.{providerErrorSummary}")
        {
            HostCapabilityEvidence = hostCapabilityEvidence
        };
    }

    private static ProcessRuntimeToolPreflightResult SatisfiedWithEvidence(
        ProcessHostCapabilityEvaluationEvidence? evidence)
        => evidence is null
            ? ProcessRuntimeToolPreflightResult.Satisfied
            : new ProcessRuntimeToolPreflightResult(true, [], string.Empty)
            {
                HostCapabilityEvidence = evidence
            };

    public async ValueTask<ProcessRuntimeToolPreflightResult> EvaluateHostCapabilitiesAsync(
        IReadOnlyList<string> requiredToolNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requiredToolNames);
        if (!IsBoundedRuntimeToolContract(requiredToolNames))
        {
            return InvalidRequirementContract("runtime-tool");
        }

        var requirements = requiredToolNames
            .Select(toolName => (
                ToolName: toolName,
                CapabilityId: ProcessRuntimeToolHostCapabilityPolicy.Resolve(toolName)))
            .Where(requirement => requirement.CapabilityId is not null)
            .Select(requirement => (requirement.ToolName, CapabilityId: requirement.CapabilityId!.Value))
            .ToArray();
        return await EvaluateHostCapabilityRequirementsAsync(requirements, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateRequiredHostCapabilitiesAsync(
        IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requiredHostCapabilities);
        if (requiredHostCapabilities.Count > MaximumRequiredHostCapabilities ||
            requiredHostCapabilities.Any(capabilityId => string.IsNullOrWhiteSpace(capabilityId.Value)))
        {
            return ValueTask.FromResult(InvalidRequirementContract("host-capability"));
        }

        var requirements = requiredHostCapabilities
            .Distinct()
            .OrderBy(capabilityId => capabilityId.Value, StringComparer.Ordinal)
            .Select(capabilityId => (ToolName: capabilityId.Value, CapabilityId: capabilityId))
            .ToArray();
        return EvaluateHostCapabilityRequirementsAsync(requirements, cancellationToken);
    }

    public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateStepHostCapabilitiesAsync(
        IReadOnlyList<string> declaredRuntimeToolNames,
        IReadOnlyList<string> effectiveRuntimeToolNames,
        IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaredRuntimeToolNames);
        ArgumentNullException.ThrowIfNull(effectiveRuntimeToolNames);
        ArgumentNullException.ThrowIfNull(requiredHostCapabilities);
        if (!IsBoundedRuntimeToolContract(declaredRuntimeToolNames) ||
            !IsBoundedRuntimeToolContract(effectiveRuntimeToolNames))
        {
            return ValueTask.FromResult(InvalidRequirementContract("runtime-tool"));
        }

        if (requiredHostCapabilities.Count > MaximumRequiredHostCapabilities ||
            requiredHostCapabilities.Any(capabilityId => string.IsNullOrWhiteSpace(capabilityId.Value)))
        {
            return ValueTask.FromResult(InvalidRequirementContract("host-capability"));
        }

        var requirements = effectiveRuntimeToolNames
            .Select(toolName => (
                ToolName: toolName,
                CapabilityId: ProcessRuntimeToolHostCapabilityPolicy.Resolve(toolName)))
            .Where(requirement => requirement.CapabilityId is not null)
            .Select(requirement => (requirement.ToolName, CapabilityId: requirement.CapabilityId!.Value))
            .Concat(requiredHostCapabilities.Select(capabilityId => (
                ToolName: capabilityId.Value,
                CapabilityId: capabilityId)))
            .GroupBy(requirement => requirement.CapabilityId)
            .Select(group => group.First())
            .OrderBy(requirement => requirement.CapabilityId.Value, StringComparer.Ordinal)
            .ToArray();
        if (requirements.Length > MaximumRequiredHostCapabilities)
        {
            return ValueTask.FromResult(InvalidRequirementContract("host-capability"));
        }

        return EvaluateHostCapabilityRequirementsAsync(requirements, cancellationToken);
    }

    private static bool IsBoundedRuntimeToolContract(IReadOnlyList<string> requiredToolNames)
        => requiredToolNames.Count <= MaximumRequiredRuntimeTools &&
            requiredToolNames.All(toolName =>
                toolName is not null &&
                toolName.Length <= MaximumRuntimeToolNameLength &&
                ProcessRequiredRuntimeToolNames.IsCanonicalRuntimeToolName(toolName));

    private static ProcessRuntimeToolPreflightResult InvalidRequirementContract(string contractKind)
        => new(
            false,
            [$"invalid-{contractKind}-contract"],
            "The process step requirement contract exceeds the supported bounded shape and was rejected before side effects.");

    private async ValueTask<ProcessRuntimeToolPreflightResult> EvaluateHostCapabilityRequirementsAsync(
        IReadOnlyList<(string ToolName, ProcessHostCapabilityId CapabilityId)> requirements,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0)
        {
            return ProcessRuntimeToolPreflightResult.Satisfied;
        }

        var snapshot = hostCapabilitySnapshotProvider is null
            ? ProcessHostCapabilitySnapshot.Unknown
            : await hostCapabilitySnapshotProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var evaluatedFacts = requirements
            .Select(requirement => requirement.CapabilityId)
            .Distinct()
            .Select(capabilityId =>
            {
                snapshot.TryGet(capabilityId, out var fact);
                return fact ?? new ProcessHostCapabilityFact(
                    capabilityId,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.NotRegistered,
                    ProcessHostExecutionPort.None);
            })
            .OrderBy(fact => fact.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var evaluatedFactsById = evaluatedFacts.ToDictionary(fact => fact.Id);
        var findings = requirements
            .Where(requirement => !evaluatedFactsById[requirement.CapabilityId].IsAvailable)
            .Select(requirement =>
            {
                var fact = evaluatedFactsById[requirement.CapabilityId];
                return new ProcessRuntimeToolHostCapabilityFinding(
                    requirement.ToolName,
                    requirement.CapabilityId,
                    fact.Availability,
                    fact.Reason,
                    snapshot.ProfileId);
            })
            .ToArray();
        var evidence = new ProcessHostCapabilityEvaluationEvidence(snapshot.ProfileId, evaluatedFacts);
        if (findings.Length == 0)
        {
            return new ProcessRuntimeToolPreflightResult(true, [], string.Empty)
            {
                HostCapabilityEvidence = evidence
            };
        }

        return new ProcessRuntimeToolPreflightResult(
            false,
            findings.Select(finding => finding.RuntimeToolName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            $"Required process host capability is unavailable on profile '{snapshot.ProfileId}': {string.Join(", ", findings.Select(finding => $"{finding.RuntimeToolName}=>{finding.CapabilityId} ({finding.Reason?.ToString() ?? "NotReported"})"))}. Configure the required host adapter or choose a compatible process strategy.")
        {
            HostCapabilityFindings = findings,
            HostCapabilityEvidence = evidence
        };
    }

    private static bool RequiresCapabilityCatalogForDiagnostics(
        IReadOnlyList<string> requiredToolNames,
        IReadOnlySet<string> contributionHandledToolNames)
    {
        return requiredToolNames.Any(requiredToolName =>
        {
            var normalizedToolName = ToolContractCatalog.NormalizeToolName(requiredToolName);
            return !string.IsNullOrWhiteSpace(normalizedToolName) &&
                   !contributionHandledToolNames.Contains(normalizedToolName) &&
                   WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.ContainsKey(normalizedToolName);
        });
    }

    private static async ValueTask<ProcessRuntimeToolPreflightRequest> ResolveCapabilityCatalogAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CapabilityCatalog is not null || request.CapabilityCatalogResolver is null)
        {
            return request;
        }

        return request with
        {
            CapabilityCatalog = await request.CapabilityCatalogResolver(cancellationToken)
                .ConfigureAwait(false)
        };
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

            if (WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.TryGetValue(
                    normalizedToolName,
                    out var workflowCapabilityKey))
            {
                AddMissingExactToolCapabilityDiagnostic(
                    request,
                    normalizedToolName,
                    workflowCapabilityKey,
                    diagnostics);
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

    private static void AddMissingExactToolCapabilityDiagnostic(
        ProcessRuntimeToolPreflightRequest request,
        string normalizedToolName,
        string capabilityKey,
        List<AgentCapabilityDiagnostic> diagnostics)
    {
        var assignments = request.Agent.Capabilities
            .Where(capability => capability.Kind == CapabilityKind.Tool)
            .Where(capability => string.Equals(
                capability.CapabilityKey,
                capabilityKey,
                StringComparison.Ordinal))
            .ToArray();
        if (assignments.Length == 1)
        {
            var assignment = assignments[0];
            var catalogMatches = (request.CapabilityCatalog ?? [])
                .Where(capability => capability.Id == assignment.CapabilityId)
                .Where(capability => capability.Kind == CapabilityKind.Tool)
                .Where(capability => string.Equals(
                    capability.Key,
                    capabilityKey,
                    StringComparison.Ordinal))
                .ToArray();
            if (catalogMatches.Length == 1)
            {
                return;
            }
        }

        diagnostics.Add(CreateMissingCapabilityDiagnostic(
            request,
            CapabilityKind.Tool,
            capabilityKey,
            $"Step '{request.Assignment.StepKey}' requires runtime tool '{normalizedToolName}', but agent '{request.Agent.Name}' does not have one exact attached tool capability '{capabilityKey}'."));
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
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog,
        AgentRuntimeContextIntent contextIntent)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            PreflightProvider,
            Capabilities: capabilityCatalog,
            SuppressApprovalRequirements: true,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            RuntimeSessionKey: string.Empty,
            contextIntent,
            Tags: new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static bool IsProviderToolAllowedForProcessIntent(
        string toolName,
        AgentRuntimeContextIntent contextIntent)
    {
        var normalizedToolName = ToolContractCatalog.NormalizeToolName(toolName);
        return !ToolCapabilityRegistry.TryResolve(normalizedToolName, out var capability) ||
               RuntimeToolProcessIntentPolicy.IsToolCapabilityAllowedForProcessIntent(
                   capability,
                   contextIntent);
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

internal static class ProcessRuntimeToolHostCapabilityPolicy
{
    public static IReadOnlySet<ProcessHostCapabilityId> ResolveAll(
        IEnumerable<string> runtimeToolNames)
    {
        ArgumentNullException.ThrowIfNull(runtimeToolNames);
        return runtimeToolNames
            .Select(Resolve)
            .Where(capabilityId => capabilityId is not null)
            .Select(capabilityId => capabilityId!.Value)
            .ToHashSet();
    }

    public static ProcessHostCapabilityId? Resolve(string runtimeToolName)
    {
        var normalized = ToolContractCatalog.NormalizeToolName(runtimeToolName);
        return normalized switch
        {
            ToolContractCatalog.WorkspacePowerShellRunScript => ProcessHostCapabilityIds.PowerShellScript,
            ToolContractCatalog.WorkspacePythonRunFile or
            ToolContractCatalog.WorkspaceInspectSpreadsheet => ProcessHostCapabilityIds.PythonRuntime,
            ToolContractCatalog.WorkspaceDotNetNew or
            ToolContractCatalog.WorkspaceDotNetRestore or
            ToolContractCatalog.WorkspaceDotNetBuild or
            ToolContractCatalog.WorkspaceDotNetTest or
            ToolContractCatalog.WorkspaceDotNetRun => ProcessHostCapabilityIds.DotNetRuntime,
            ToolContractCatalog.WorkspaceDotNetStop => ProcessHostCapabilityIds.DirectExecution,
            ToolContractCatalog.LocalMcpLaunch => ProcessHostCapabilityIds.LocalStdioMcp,
            ToolContractCatalog.WorkspaceCommandRun or
            ToolContractCatalog.WorkspaceGitDiff or
            ToolContractCatalog.WorkspaceGitStatus or
            ToolContractCatalog.WorkspaceGitLog or
            ToolContractCatalog.WorkspaceGitShow or
            ToolContractCatalog.WorkspaceGitAdd or
            ToolContractCatalog.WorkspaceGitUnstage or
            ToolContractCatalog.WorkspaceGitCommit or
            ToolContractCatalog.WorkspaceGitBranchCreate or
            ToolContractCatalog.WorkspaceGitSwitch or
            AgentToolInvocationPolicyMetadata.RunSkillScript => ProcessHostCapabilityIds.DirectExecution,
            _ => null
        };
    }
}
