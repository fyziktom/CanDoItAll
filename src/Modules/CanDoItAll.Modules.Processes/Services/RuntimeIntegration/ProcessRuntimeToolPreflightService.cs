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

        var context = CreateProviderContext(request.Assignment, request.Agent);
        var composedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    private static AgentRuntimeToolProviderContext CreateProviderContext(
        ProcessRuntimeStepAssignment assignment,
        AgentDefinition agent)
    {
        var contextIntent = new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: assignment.StepKey,
            ProcessRunId: assignment.RunId.Value.ToString("D"),
            ProcessStepId: assignment.StepInstanceId.Value.ToString("D"),
            TargetScope: assignment.OperationTargetScope,
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: true,
            ScaffoldToolOnly: false,
            AllowsProductMutation: assignment.AllowedOperations.Any(operation =>
                string.Equals(operation, ProcessOperationContractNames.MutateProductTarget, StringComparison.OrdinalIgnoreCase)),
            WorkspaceToolProfile: null,
            WorkspaceScope: null,
            AllowedOperations: assignment.AllowedOperations,
            RuntimeToolProvidersEnabled: true,
            WorkspaceToolsEnabled: true,
            CapabilityScopeOverride: AgentFrameworkProcessCapabilityScopeTranslator.Translate(assignment.CapabilityScope));

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
}
