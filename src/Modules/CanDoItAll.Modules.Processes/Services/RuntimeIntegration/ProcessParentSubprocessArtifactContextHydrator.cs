using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessParentArtifactContextHydration(
    string PromptContribution,
    ProcessCompletionIssue? Issue)
{
    internal static readonly ProcessParentArtifactContextHydration Empty = new(string.Empty, null);
}

internal sealed class ProcessParentSubprocessArtifactContextHydrator(IWorkspaceFileService workspaceFiles)
{
    internal const string MissingContextDiagnosticCode = "process.adapter.parent_required_artifact_context_missing";
    private const int MaxArtifactCount = 16;
    private const int MaxTotalCharacters = 64_000;
    private const int MinCharactersPerArtifact = 2_000;
    private const int MaxCharactersPerArtifact = 16_000;

    internal ProcessParentArtifactContextHydration Hydrate(ProcessRuntimeStepAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (!ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactRefs(
                assignment.LaunchVariables,
                out var artifactRefs))
        {
            return ProcessParentArtifactContextHydration.Empty;
        }

        if (artifactRefs.Count > MaxArtifactCount)
        {
            return CreateFailure(
                assignment,
                artifactRefs,
                $"Inherited parent artifact count {artifactRefs.Count} exceeds the bounded context limit {MaxArtifactCount}.");
        }

        var maxCharactersPerArtifact = Math.Clamp(
            MaxTotalCharacters / artifactRefs.Count,
            MinCharactersPerArtifact,
            MaxCharactersPerArtifact);
        var hydratedArtifacts = new List<(string Ref, string Content, bool IsTruncated)>();
        var failures = new List<string>();
        foreach (var artifactRef in artifactRefs)
        {
            var readResult = workspaceFiles.ReadTextFile(artifactRef, maxCharactersPerArtifact);
            if (!readResult.Succeeded || string.IsNullOrWhiteSpace(readResult.Content))
            {
                failures.Add($"{artifactRef}: {readResult.Message}");
                continue;
            }

            hydratedArtifacts.Add((artifactRef, readResult.Content, readResult.IsTruncated));
        }

        if (failures.Count > 0)
        {
            return CreateFailure(
                assignment,
                artifactRefs,
                $"The runtime could not hydrate required inherited parent artifact context: {string.Join("; ", failures)}");
        }

        var contribution = new StringBuilder()
            .AppendLine("Runtime-hydrated inherited parent-step artifact content:")
            .AppendLine("The process adapter loaded every exact inherited ref before this agent invocation. Treat this bounded content as required upstream evidence. Use workspace_read_file only when a section is marked truncated and more detail is needed.");
        foreach (var artifact in hydratedArtifacts)
        {
            contribution
                .AppendLine()
                .Append("----- BEGIN INHERITED ARTIFACT: ")
                .Append(artifact.Ref)
                .AppendLine(" -----")
                .AppendLine(artifact.Content.TrimEnd())
                .Append("----- END INHERITED ARTIFACT: ")
                .Append(artifact.Ref)
                .AppendLine(" -----");
            if (artifact.IsTruncated)
            {
                contribution.AppendLine("[Runtime note: content was truncated to the bounded prompt budget; read the exact ref for additional detail.]");
            }
        }

        return new ProcessParentArtifactContextHydration(contribution.ToString().TrimEnd(), null);
    }

    private static ProcessParentArtifactContextHydration CreateFailure(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> artifactRefs,
        string summary)
    {
        var orderedRefs = artifactRefs
            .OrderBy(artifactRef => artifactRef, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessParentArtifactContextHydration(
            string.Empty,
            new ProcessCompletionIssue(
                MissingContextDiagnosticCode,
                summary,
                $"{assignment.RunId}:{assignment.StepInstanceId}:parent-required-artifact-context-missing:{string.Join("|", orderedRefs)}",
                assignment.RequiredArtifactSlotIds,
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
    }
}
