using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRuntimeToolPreflightContribution
{
    string ContributionKey { get; }

    int Order { get; }

    void Contribute(ProcessRuntimeToolPreflightContributionContext context);
}

internal sealed class ProcessRuntimeToolPreflightContributionContext
{
    private readonly HashSet<string> requiredToolNames;
    private readonly HashSet<string> handledToolNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AgentCapabilityDiagnostic> capabilityDiagnostics = [];
    private readonly HashSet<string> composedToolNames = new(StringComparer.OrdinalIgnoreCase);

    internal ProcessRuntimeToolPreflightContributionContext(
        ProcessRuntimeToolPreflightRequest request,
        IReadOnlyList<string> requiredToolNames,
        AgentRuntimeContextIntent contextIntent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requiredToolNames);
        ArgumentNullException.ThrowIfNull(contextIntent);

        Request = request;
        RequiredToolNames = requiredToolNames
            .Select(ToolContractCatalog.NormalizeToolName)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.Ordinal)
            .ToArray();
        this.requiredToolNames = new HashSet<string>(RequiredToolNames, StringComparer.OrdinalIgnoreCase);
        ContextIntent = contextIntent;
    }

    public ProcessRuntimeToolPreflightRequest Request { get; }

    public IReadOnlyList<string> RequiredToolNames { get; }

    public AgentRuntimeContextIntent ContextIntent { get; private set; }

    public IReadOnlySet<string> HandledToolNames => handledToolNames;

    public IReadOnlyList<AgentCapabilityDiagnostic> CapabilityDiagnostics => capabilityDiagnostics;

    public IReadOnlySet<string> ComposedToolNames => composedToolNames;

    public void ReplaceContextIntent(AgentRuntimeContextIntent contextIntent)
    {
        ArgumentNullException.ThrowIfNull(contextIntent);

        ContextIntent = contextIntent;
    }

    public void MarkToolHandled(string normalizedToolName)
    {
        var toolName = RequireDeclaredToolName(normalizedToolName, "handled");
        if (!handledToolNames.Add(toolName))
        {
            throw new InvalidOperationException(
                $"Runtime tool '{toolName}' is handled by more than one process preflight contribution.");
        }
    }

    public void AddCapabilityDiagnostic(AgentCapabilityDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        capabilityDiagnostics.Add(diagnostic);
    }

    public void AddComposedToolName(string normalizedToolName)
    {
        var toolName = RequireDeclaredToolName(normalizedToolName, "claimed as composed");
        if (!handledToolNames.Contains(toolName))
        {
            throw new InvalidOperationException(
                $"Runtime tool '{toolName}' must be marked as handled before it can be claimed as composed.");
        }

        composedToolNames.Add(toolName);
    }

    private string RequireDeclaredToolName(string toolName, string action)
    {
        var normalizedToolName = ToolContractCatalog.NormalizeToolName(toolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            throw new ArgumentException("A contributed runtime tool name is required.", nameof(toolName));
        }

        if (!requiredToolNames.Contains(normalizedToolName))
        {
            throw new InvalidOperationException(
                $"Runtime tool '{normalizedToolName}' is not required by the current process preflight request and cannot be {action}.");
        }

        return normalizedToolName;
    }
}

internal sealed class ProcessRuntimeToolPreflightContributionCatalog(
    IEnumerable<IProcessRuntimeToolPreflightContribution> contributions)
{
    public static ProcessRuntimeToolPreflightContributionCatalog Empty { get; } = new([]);

    private readonly IReadOnlyList<IProcessRuntimeToolPreflightContribution> contributions = CreateContributions(contributions);

    internal void Contribute(ProcessRuntimeToolPreflightContributionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var contribution in contributions)
        {
            contribution.Contribute(context);
        }
    }

    private static IReadOnlyList<IProcessRuntimeToolPreflightContribution> CreateContributions(
        IEnumerable<IProcessRuntimeToolPreflightContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var materialized = contributions.ToArray();
        if (materialized.Any(contribution => contribution is null))
        {
            throw new InvalidOperationException(
                "A process runtime tool preflight contribution registration cannot be null.");
        }

        var ordered = materialized
            .OrderBy(contribution => contribution.Order)
            .ThenBy(contribution => contribution.ContributionKey, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(contribution => string.IsNullOrWhiteSpace(contribution.ContributionKey)))
        {
            throw new InvalidOperationException(
                "A process runtime tool preflight contribution must declare a stable contribution key.");
        }

        var duplicate = ordered
            .GroupBy(contribution => contribution.ContributionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate process runtime tool preflight contribution key '{duplicate.Key}' is registered.");
        }

        return ordered;
    }
}
