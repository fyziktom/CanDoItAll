using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRequiredToolValidationRules
{
    internal static ProcessRequiredToolDecision ResolveMissingRequiredTools(
        ProcessRequiredToolValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requiredToolNames = request.DeclaredRequiredToolNames
            .Concat(request.MetadataRequiredToolNames)
            .Select(ProcessToolReceiptFacts.NormalizeToolToken)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requiredToolNames.Count == 0)
        {
            return new ProcessRequiredToolDecision([], [], [], [], []);
        }

        var successfulToolNames = request.SuccessfulToolNames
            .Select(ProcessToolReceiptFacts.NormalizeToolToken)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);
        var carriedForwardToolNames = new List<string>();
        foreach (var toolName in request.SuccessfulToolNamesFromPriorAttempts)
        {
            var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(toolName);
            if (!string.IsNullOrWhiteSpace(normalizedToolName) &&
                ShouldCarryForwardSuccessfulToolName(
                    request.Policy,
                    request.RequiresConcreteImplementationProof,
                    request.RequiresConcreteBrowserProof,
                    normalizedToolName))
            {
                successfulToolNames.Add(normalizedToolName);
                carriedForwardToolNames.Add(normalizedToolName);
            }
        }

        var processMockSatisfiedToolNames = request.ProcessMockSatisfiedToolNames
            .Select(ProcessToolReceiptFacts.NormalizeToolToken)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var toolName in processMockSatisfiedToolNames)
        {
            successfulToolNames.Add(toolName);
        }

        var missingToolNames = requiredToolNames
            .Where(requiredToolName => !successfulToolNames.Contains(requiredToolName))
            .ToList();
        if (missingToolNames.Contains(ToolContractCatalog.WorkspaceDotNetNew, StringComparer.Ordinal) &&
            request.CanSatisfyMissingDotnetNewWithValidatedExistingScaffold)
        {
            missingToolNames = missingToolNames
                .Where(toolName => !string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetNew, StringComparison.Ordinal))
                .ToList();
        }

        if (request.CanSatisfyImplementationProofToolsWithCarriedProof)
        {
            missingToolNames = missingToolNames
                .Where(toolName =>
                    !ContainsToolName(request.Policy.ImplementationProofToolNames, toolName) &&
                    !request.Policy.IsImplementationValidationToolName(toolName) &&
                    !(string.Equals(toolName, ToolContractCatalog.WorkspaceWriteFile, StringComparison.Ordinal) &&
                      request.CanSatisfyImplementationArtifactWriteWithRecordedArtifacts))
                .ToList();
        }

        return new ProcessRequiredToolDecision(
            requiredToolNames,
            successfulToolNames.OrderBy(toolName => toolName, StringComparer.Ordinal).ToList(),
            carriedForwardToolNames.Distinct(StringComparer.Ordinal).ToList(),
            processMockSatisfiedToolNames,
            missingToolNames);
    }

    internal static bool ShouldCarryForwardSuccessfulToolName(
        ProcessRequiredToolValidationPolicy policy,
        bool requiresConcreteImplementationProof,
        bool requiresConcreteBrowserProof,
        string normalizedToolName)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        if (requiresConcreteImplementationProof &&
            IsCurrentAttemptOnlyImplementationToolName(policy, normalizedToolName))
        {
            return false;
        }

        if (requiresConcreteBrowserProof &&
            ContainsToolName(policy.CurrentAttemptOnlyBrowserProofToolNames, normalizedToolName))
        {
            return false;
        }

        return true;
    }

    internal static bool IsCurrentAttemptOnlyImplementationToolName(
        ProcessRequiredToolValidationPolicy policy,
        string normalizedToolName)
    {
        return ContainsToolName(policy.CurrentAttemptOnlyImplementationProofToolNames, normalizedToolName) ||
               policy.IsImplementationValidationToolName(normalizedToolName);
    }

    internal static bool HasUnrecoverableMissingRequiredTool(
        ProcessRequiredToolValidationPolicy policy,
        IReadOnlyList<string> missingRequiredTools)
    {
        return missingRequiredTools.Any(toolName =>
            !ContainsToolName(policy.ImplementationProofToolNames, toolName) &&
            !ContainsToolName(policy.ConcreteProductMutationToolNames, toolName) &&
            !toolName.StartsWith("project_structure_", StringComparison.Ordinal) &&
            !policy.IsImplementationValidationToolName(toolName));
    }

    private static bool ContainsToolName(IReadOnlyCollection<string> toolNames, string toolName)
    {
        return toolNames.Contains(toolName, StringComparer.Ordinal);
    }
}

internal sealed record ProcessRequiredToolValidationRequest(
    IReadOnlyList<string> DeclaredRequiredToolNames,
    IReadOnlyList<string> MetadataRequiredToolNames,
    ISet<string> SuccessfulToolNames,
    IEnumerable<string> SuccessfulToolNamesFromPriorAttempts,
    IReadOnlyList<string> ProcessMockSatisfiedToolNames,
    bool RequiresConcreteImplementationProof,
    bool RequiresConcreteBrowserProof,
    bool CanSatisfyMissingDotnetNewWithValidatedExistingScaffold,
    bool CanSatisfyImplementationProofToolsWithCarriedProof,
    bool CanSatisfyImplementationArtifactWriteWithRecordedArtifacts,
    ProcessRequiredToolValidationPolicy Policy);

internal sealed record ProcessRequiredToolValidationPolicy(
    IReadOnlyCollection<string> ImplementationProofToolNames,
    IReadOnlyCollection<string> ConcreteProductMutationToolNames,
    IReadOnlyCollection<string> CurrentAttemptOnlyImplementationProofToolNames,
    IReadOnlyCollection<string> CurrentAttemptOnlyBrowserProofToolNames,
    Func<string, bool> IsImplementationValidationToolName);

internal sealed record ProcessRequiredToolDecision(
    IReadOnlyList<string> RequiredToolNames,
    IReadOnlyList<string> SuccessfulToolNames,
    IReadOnlyList<string> CarriedForwardToolNames,
    IReadOnlyList<string> ProcessMockSatisfiedToolNames,
    IReadOnlyList<string> MissingToolNames);
