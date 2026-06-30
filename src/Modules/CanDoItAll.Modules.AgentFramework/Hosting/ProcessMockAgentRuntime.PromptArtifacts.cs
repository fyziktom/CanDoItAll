using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed partial class ProcessMockAgentRuntime
{
    private string BuildImplementationRequiredArtifactSections(
        ProcessMockRuntimeState state,
        bool repaired,
        List<ProcessMockRuntimeArtifact> artifacts)
    {
        var sections = new List<string>();
        if (PromptRequiresArtifact(state.OriginalPrompt, "Implementation change set"))
        {
            var changeSetPath = repaired
                ? $"{state.ArtifactRoot}/05-implementation-change-set.md"
                : $"{state.ArtifactRoot}/03-implementation-change-set.md";
            var changeSetMarkdown =
                $$"""
                # Implementation Change Set

                ## Touched Surface Inventory
                - `{{state.OutputRoot}}/MockApp/ValidationEngine.cs` contains the mock validation implementation.

                ## Tests And Validation
                - Deterministic process mock validation stands in for the implementation agent proof path.
                - The change set is linked to validation behavior tests and migration notes by this governed artifact.

                ## Migration Notes
                - No schema or data migration is introduced by the mock implementation.
                """;
            fileService.WriteTextFile(changeSetPath, changeSetMarkdown, overwrite: true);
            artifacts.Add(CreateArtifact(
                changeSetPath,
                "implementation change set tests migration notes touched surface inventory"));
            sections.Add(
                """
                ## Implementation change set
                Touched surface inventory: ValidationEngine owns name normalization and blank-input validation behavior for the mock implementation target.
                Tests and validation: deterministic process mock validation covers the implementation lane and links the change set to test proof.
                Migration notes: no schema, persistent data, or backfill changes are part of this implementation.
                """);
        }

        if (PromptRequiresArtifact(state.OriginalPrompt, "Migration and rollout preparation checklist"))
        {
            var checklistPath = repaired
                ? $"{state.ArtifactRoot}/05-migration-rollout-preparation-checklist.md"
                : $"{state.ArtifactRoot}/03-migration-rollout-preparation-checklist.md";
            var checklistMarkdown =
                """
                # Migration And Rollout Preparation Checklist

                ## Data Changes
                - No data migration required.
                - No schema migration, seed update, backfill, or data rollback step is required.

                ## Operational Preconditions
                - Implementation validation must pass before rollout.
                - QA must verify name normalization and blank-input behavior before release.

                ## Rollback Steps
                - Revert the implementation change set or restore the previous project state.
                - No data rollback is required because no persistent data changes are introduced.
                """;
            fileService.WriteTextFile(checklistPath, checklistMarkdown, overwrite: true);
            artifacts.Add(CreateArtifact(
                checklistPath,
                "migration rollout preparation checklist data changes operational preconditions rollback steps no data migration required"));
            sections.Add(
                """
                ## Migration and rollout preparation checklist
                Data changes: no data migration required; no schema migration, seed update, backfill, or data rollback is needed.
                Operational preconditions: implementation validation must pass and QA must verify name normalization plus blank-input behavior.
                Rollback steps: revert the implementation change set or restore the previous project state; no data rollback is required.
                """);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private string BuildPromptRequiredArtifactSections(
        ProcessMockRuntimeState state,
        string sequencePrefix,
        List<ProcessMockRuntimeArtifact> artifacts)
    {
        var requiredArtifacts = ResolvePromptRequiredArtifacts(state.OriginalPrompt);
        if (requiredArtifacts.Count == 0)
        {
            return string.Empty;
        }

        fileService.CreateDirectory(state.ArtifactRoot);

        var sections = new List<string>();
        for (var index = 0; index < requiredArtifacts.Count; index++)
        {
            var requiredArtifact = requiredArtifacts[index];
            var relativePath = ResolvePromptRequiredArtifactPath(
                state,
                sequencePrefix,
                index + 1,
                requiredArtifact);
            if (RequiresPromptImageArtifactFile(requiredArtifact))
            {
                WriteWorkspaceBytes(relativePath, MockBrowserScreenshotPngBytes);
            }
            else
            {
                var markdown =
                    $"""
                    # {requiredArtifact.Title}

                    Process mock required output artifact.

                    Validation expectation: {NormalizePromptSummary(requiredArtifact.ValidationRequirementSummary)}
                    Outcome: Required artifact contract is satisfied for deterministic automation dispatch, finalizer projection, and readback validation.
                    """;
                WriteWorkspaceText(relativePath, markdown);
            }

            var sectionSummary =
                $"""
                Validation expectation: {NormalizePromptSummary(requiredArtifact.ValidationRequirementSummary)}
                Outcome: Required artifact contract is satisfied for deterministic automation dispatch, finalizer projection, and readback validation.
                """;

            artifacts.Add(CreateArtifact(
                relativePath,
                BuildPromptRequiredArtifactSignalText(requiredArtifact)));
            sections.Add(
                $"""
                ## {requiredArtifact.Title}
                {sectionSummary}
                """);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string ResolvePromptRequiredArtifactPath(
        ProcessMockRuntimeState state,
        string sequencePrefix,
        int sequence,
        PromptRequiredArtifact requiredArtifact)
    {
        var promptPath = FirstNonEmpty(requiredArtifact.ManagedPath, requiredArtifact.GovernedPath);
        if (TryNormalizeRelativePromptPath(promptPath, out var relativePath))
        {
            return RequiresPromptImageArtifactFile(requiredArtifact) &&
                   !IsPromptImageArtifactExtension(Path.GetExtension(relativePath).ToLowerInvariant())
                ? WorkspaceScopeDescriptor.NormalizeRelativePath(Path.ChangeExtension(relativePath, ".png") ?? relativePath)
                : relativePath;
        }

        var extension = RequiresPromptImageArtifactFile(requiredArtifact)
            ? ".png"
            : ".md";
        return $"{state.ArtifactRoot}/{sequencePrefix}-{sequence:00}-{Slugify(requiredArtifact.Title)}{extension}";
    }

    private static bool RequiresPromptImageArtifactFile(PromptRequiredArtifact requiredArtifact)
    {
        var titleText = NormalizePromptArtifactSignalText(requiredArtifact.Title).ToLowerInvariant();
        if (IsPromptNarrativeArtifactContainerTitle(titleText))
        {
            return false;
        }

        if (ContainsPromptImageEvidenceToken(titleText))
        {
            return true;
        }

        if (IsPromptImageArtifactExtension(Path.GetExtension(requiredArtifact.ManagedPath ?? string.Empty).ToLowerInvariant()) ||
            IsPromptImageArtifactExtension(Path.GetExtension(requiredArtifact.GovernedPath ?? string.Empty).ToLowerInvariant()))
        {
            return true;
        }

        var contractText = NormalizePromptArtifactSignalText(
            $"{requiredArtifact.ValidationRequirementSummary} {requiredArtifact.ManagedPath} {requiredArtifact.GovernedPath}").ToLowerInvariant();
        return ContainsExplicitPromptImageFileSignal(contractText);
    }

    private static bool IsPromptImageArtifactExtension(string extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".svg";
    }

    private static bool ContainsPromptImageEvidenceToken(string text)
    {
        return text.Contains("screenshot", StringComparison.Ordinal) ||
               text.Contains("image", StringComparison.Ordinal);
    }

    private static bool ContainsExplicitPromptImageFileSignal(string text)
    {
        return text.Contains(".png", StringComparison.Ordinal) ||
               text.Contains(".jpg", StringComparison.Ordinal) ||
               text.Contains(".jpeg", StringComparison.Ordinal) ||
               text.Contains(".webp", StringComparison.Ordinal) ||
               text.Contains(".svg", StringComparison.Ordinal) ||
               text.Contains("image file", StringComparison.Ordinal) ||
               text.Contains("screenshot file", StringComparison.Ordinal) ||
               text.Contains("image artifact", StringComparison.Ordinal) ||
               text.Contains("screenshot artifact", StringComparison.Ordinal) ||
               text.Contains("as an image", StringComparison.Ordinal) ||
               text.Contains("as a screenshot", StringComparison.Ordinal);
    }

    private static bool IsPromptNarrativeArtifactContainerTitle(string titleText)
    {
        return titleText.Contains("pack", StringComparison.Ordinal) ||
               titleText.Contains("summary", StringComparison.Ordinal) ||
               titleText.Contains("report", StringComparison.Ordinal) ||
               titleText.Contains("index", StringComparison.Ordinal) ||
               titleText.Contains("log", StringComparison.Ordinal) ||
               titleText.Contains("manifest", StringComparison.Ordinal) ||
               titleText.Contains("list", StringComparison.Ordinal) ||
               titleText.Contains("writeback", StringComparison.Ordinal);
    }

    private static string NormalizePromptArtifactSignalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildPromptRequiredArtifactSignalText(PromptRequiredArtifact requiredArtifact)
    {
        return string.Join(
            ' ',
            requiredArtifact.Title,
            requiredArtifact.ArtifactKind,
            "process mock required output artifact current run finalizer readback");
    }

    private static IReadOnlyList<PromptRequiredArtifact> ResolvePromptRequiredArtifacts(string prompt)
    {
        var artifacts = new List<PromptRequiredArtifactBuilder>();
        PromptRequiredArtifactBuilder? current = null;
        var inSection = false;

        foreach (var rawLine in prompt.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd();
            if (string.Equals(line.Trim(), "Required output artifacts:", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (current is not null || artifacts.Count > 0)
                {
                    break;
                }

                continue;
            }

            var artifactMatch = RequiredArtifactLineRegex().Match(line.Trim());
            if (artifactMatch.Success)
            {
                current = new PromptRequiredArtifactBuilder(
                    artifactMatch.Groups["title"].Value.Trim(),
                    artifactMatch.Groups["kind"].Value.Trim());
                artifacts.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var trimmed = line.Trim();
            if (TryReadPromptField(trimmed, "Validation:", out var validation))
            {
                current.ValidationRequirementSummary = validation;
            }
            else if (TryReadPromptField(trimmed, "Managed path:", out var managedPath))
            {
                current.ManagedPath = managedPath;
            }
            else if (TryReadPromptField(trimmed, "Governed path:", out var governedPath))
            {
                current.GovernedPath = governedPath;
            }
        }

        return artifacts
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => new PromptRequiredArtifact(
                item.Title,
                item.ArtifactKind,
                item.ValidationRequirementSummary,
                item.ManagedPath,
                item.GovernedPath))
            .ToArray();
    }

    private static bool TryReadPromptField(string line, string fieldName, out string value)
    {
        if (line.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
        {
            value = line[fieldName.Length..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryNormalizeRelativePromptPath(string? value, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().Trim('`').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return false;
        }

        relativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(candidate);
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static string MergeSummary(string primary, string secondary)
    {
        if (string.IsNullOrWhiteSpace(primary))
        {
            return secondary;
        }

        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        return primary + Environment.NewLine + Environment.NewLine + secondary;
    }

    private static string NormalizePromptSummary(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "No additional validation summary was provided by the prompt."
            : value.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray();
        var collapsed = string.Join(
            "-",
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(collapsed)
            ? "artifact"
            : collapsed;
    }

    private static bool PromptRequiresArtifact(string prompt, string artifactTitle)
    {
        return prompt.Contains(artifactTitle, StringComparison.OrdinalIgnoreCase);
    }

}
