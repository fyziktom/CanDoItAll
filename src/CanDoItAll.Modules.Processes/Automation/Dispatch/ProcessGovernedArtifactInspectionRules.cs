using CanDoItAll.AgentFramework.Models;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using DispatchArtifactInput = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactInput;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessGovernedInspectionPathSet(
    IReadOnlyList<string> StatPaths,
    IReadOnlyList<string> ReadPaths);

internal static class ProcessGovernedArtifactInspectionRules
{
    internal static ProcessGovernedInspectionPathSet ResolveGovernedInspectionPaths(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expectedArtifact in expectedArtifacts)
        {
            if (!ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
                    expectedArtifact.ValidationRequirementSummary,
                    out var relativePath))
            {
                continue;
            }

            var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            statPaths.Add(normalizedPath);
            if (ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath(normalizedPath))
            {
                readPaths.Add(normalizedPath);
            }
        }

        return new ProcessGovernedInspectionPathSet(statPaths.ToList(), readPaths.ToList());
    }

    internal static ProcessGovernedInspectionPathSet ResolveArtifactInputInspectionPaths(
        IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceStepGroup in artifactInputs.GroupBy(input => input.SourceStepTitle, StringComparer.OrdinalIgnoreCase))
        {
            var sourceStepStatPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceStepReadPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceStepVisualAttachmentPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifactInput in sourceStepGroup)
            {
                foreach (var artifact in artifactInput.Artifacts)
                {
                    var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.ManagedStoragePath);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath(normalizedPath))
                    {
                        sourceStepStatPaths.Add(normalizedPath);
                        sourceStepReadPaths.Add(normalizedPath);
                        continue;
                    }

                    if (ProcessManagedArtifactPathClassificationRules.IsVisualEvidenceAttachmentPath(normalizedPath))
                    {
                        sourceStepVisualAttachmentPaths.Add(normalizedPath);
                        continue;
                    }

                    sourceStepStatPaths.Add(normalizedPath);
                }
            }

            foreach (var path in sourceStepStatPaths)
            {
                statPaths.Add(path);
            }

            foreach (var path in sourceStepReadPaths)
            {
                readPaths.Add(path);
            }

            if (sourceStepReadPaths.Count == 0)
            {
                foreach (var path in sourceStepVisualAttachmentPaths)
                {
                    statPaths.Add(path);
                }
            }
        }

        return new ProcessGovernedInspectionPathSet(statPaths.ToList(), readPaths.ToList());
    }

    internal static ProcessGovernedInspectionPathSet ResolveMissingUpstreamArtifactInspectionPaths(
        bool requiresGovernedInspection,
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        IReadOnlyList<string> successfulSessionStatPaths,
        IReadOnlyList<string> successfulSessionReadPaths,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> toolReceipts,
        Func<ProcessAutomationToolExecutionReceipt, IReadOnlyList<string>> resolveManagedWorkspacePathsFromReceipt)
    {
        ArgumentNullException.ThrowIfNull(successfulSessionStatPaths);
        ArgumentNullException.ThrowIfNull(successfulSessionReadPaths);
        ArgumentNullException.ThrowIfNull(toolReceipts);
        ArgumentNullException.ThrowIfNull(resolveManagedWorkspacePathsFromReceipt);

        if (!requiresGovernedInspection || artifactInputs.Count == 0)
        {
            return new ProcessGovernedInspectionPathSet([], []);
        }

        var requiredInspectionPaths = ResolveArtifactInputInspectionPaths(artifactInputs);
        if (requiredInspectionPaths.StatPaths.Count == 0 && requiredInspectionPaths.ReadPaths.Count == 0)
        {
            return new ProcessGovernedInspectionPathSet([], []);
        }

        var successfulStatPaths = ResolveSuccessfulWorkspaceInspectionPaths(
            toolReceipts,
            "workspace_stat_path",
            successfulSessionStatPaths,
            resolveManagedWorkspacePathsFromReceipt);
        var successfulReadPaths = ResolveSuccessfulWorkspaceInspectionPaths(
            toolReceipts,
            "workspace_read_file",
            successfulSessionReadPaths,
            resolveManagedWorkspacePathsFromReceipt);

        var missingStatPaths = requiredInspectionPaths.StatPaths
            .Where(path => !ContainsEquivalentManagedPath(successfulStatPaths, path) &&
                           !ContainsEquivalentManagedPath(successfulReadPaths, path))
            .Take(3)
            .ToList();
        var missingReadPaths = requiredInspectionPaths.ReadPaths
            .Where(path => !ContainsEquivalentManagedPath(successfulReadPaths, path))
            .Take(3)
            .ToList();

        return new ProcessGovernedInspectionPathSet(missingStatPaths, missingReadPaths);
    }

    internal static string ResolveMissingUpstreamArtifactInspectionSummary(ProcessGovernedInspectionPathSet missingInspectionPaths)
    {
        if (missingInspectionPaths.StatPaths.Count == 0 && missingInspectionPaths.ReadPaths.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (missingInspectionPaths.StatPaths.Count > 0)
        {
            parts.Add($"workspace_stat_path missing for {FormatPromptPathList(missingInspectionPaths.StatPaths)}");
        }

        if (missingInspectionPaths.ReadPaths.Count > 0)
        {
            parts.Add($"workspace_read_file missing for {FormatPromptPathList(missingInspectionPaths.ReadPaths)}");
        }

        return "the review step did not directly inspect inherited upstream artifacts: " + string.Join("; ", parts);
    }

    internal static string FormatPromptPathList(IReadOnlyList<string> relativePaths)
    {
        return string.Join(", ", relativePaths.Select(relativePath => $"`{relativePath}`"));
    }

    private static bool ContainsEquivalentManagedPath(IReadOnlySet<string> paths, string requiredPath)
    {
        if (paths.Contains(requiredPath))
        {
            return true;
        }

        var normalizedRequiredPath = ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(requiredPath);
        return !string.IsNullOrWhiteSpace(normalizedRequiredPath) &&
               paths.Any(path => string.Equals(
                   ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(path),
                   normalizedRequiredPath,
                   StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlySet<string> ResolveSuccessfulWorkspaceInspectionPaths(
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> toolReceipts,
        string normalizedToolName,
        IReadOnlyList<string> sessionPaths,
        Func<ProcessAutomationToolExecutionReceipt, IReadOnlyList<string>> resolveManagedWorkspacePathsFromReceipt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sessionPath in sessionPaths)
        {
            AddNormalizedWorkspaceInspectionPath(paths, sessionPath);
        }

        foreach (var receipt in toolReceipts.Where(receipt =>
                     !ProcessToolReceiptFacts.IsFailedReceipt(receipt) &&
                     string.Equals(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName), normalizedToolName, StringComparison.Ordinal)))
        {
            foreach (var path in resolveManagedWorkspacePathsFromReceipt(receipt))
            {
                AddNormalizedWorkspaceInspectionPath(paths, path);
            }
        }

        return paths;
    }

    private static void AddNormalizedWorkspaceInspectionPath(ISet<string> paths, string path)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            paths.Add(normalizedPath);
        }
    }
}
