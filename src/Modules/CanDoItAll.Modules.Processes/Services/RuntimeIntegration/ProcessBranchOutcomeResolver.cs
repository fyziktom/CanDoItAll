using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessBranchOutcomeResolver
{
    internal static bool ShouldRouteBlockedBranchOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => output.Status == ProcessStepOutcomeStatus.Blocked &&
           !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) &&
           (assignment.ProducedArtifactSlotIds.Count == 0 || output.EvidenceRefs.Count > 0);

    internal static bool TryInferEvidenceBackedBranchOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out string branchOutcomeKey)
    {
        branchOutcomeKey = string.Empty;
        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.Completed) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            assignment.ProducedArtifactSlotIds.Count > 0 && output.EvidenceRefs.Count == 0)
        {
            return false;
        }

        var outputTextParts = EnumerateOutcomeText(output)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var outputText = string.Join(" ", outputTextParts);
        var declaredBranchOutcomes = EnumerateDeclaredBranchOutcomes(assignment.Prompt)
            .GroupBy(outcome => outcome.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (TryReadExplicitBranchOutcomeKey(string.Join(Environment.NewLine, outputTextParts), declaredBranchOutcomes, out branchOutcomeKey))
        {
            return true;
        }

        if (TryReadBranchOutcomeDecisionSection(string.Join(Environment.NewLine, outputTextParts), declaredBranchOutcomes, out branchOutcomeKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(outputText) ||
            LooksLikeRightsOrToolBoundary(outputText) ||
            !LooksLikeBranchSelectionText(outputText))
        {
            return false;
        }

        var mentionedBranchKeys = declaredBranchOutcomes
            .Where(outcome => ContainsBranchOutcomeKey(outputText, outcome.Key))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mentionedBranchKeys.Length == 1)
        {
            branchOutcomeKey = mentionedBranchKeys[0];
            return true;
        }

        if (mentionedBranchKeys.Length > 1)
        {
            return false;
        }

        var mentionedBranchTitles = declaredBranchOutcomes
            .Where(outcome => ContainsBranchOutcomeTitle(outputText, outcome.Title))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mentionedBranchTitles.Length != 1)
        {
            return false;
        }

        branchOutcomeKey = mentionedBranchTitles[0];
        return true;
    }

    internal static bool TryReadExplicitBranchOutcomeKey(
        string text,
        IReadOnlyCollection<BranchOutcomePromptDescriptor> declaredBranchOutcomes,
        out string branchOutcomeKey)
    {
        branchOutcomeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text) ||
            declaredBranchOutcomes.Count == 0)
        {
            return false;
        }

        var explicitKeys = ReadExplicitBranchOutcomeKeys(text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (explicitKeys.Length != 1)
        {
            return false;
        }

        var declaredMatches = declaredBranchOutcomes
            .Where(outcome => string.Equals(outcome.Key, explicitKeys[0], StringComparison.OrdinalIgnoreCase))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (declaredMatches.Length != 1)
        {
            return false;
        }

        branchOutcomeKey = declaredMatches[0];
        return true;
    }

    internal static string ResolveProducedArtifactContentHash(
        ArtifactSlotId slotId,
        IReadOnlyDictionary<ArtifactSlotId, string>? producedArtifactContentHashes,
        string rawOutputHash,
        ProcessStepInstanceId stepInstanceId)
    {
        if (producedArtifactContentHashes is not null &&
            producedArtifactContentHashes.TryGetValue(slotId, out var contentHash) &&
            !string.IsNullOrWhiteSpace(contentHash))
        {
            return contentHash;
        }

        return ComputeHash($"{rawOutputHash}:{stepInstanceId}:{slotId}");
    }

    internal static bool TryReadBranchOutcomeDecisionSection(
        string text,
        IReadOnlyCollection<BranchOutcomePromptDescriptor> declaredBranchOutcomes,
        out string branchOutcomeKey)
    {
        branchOutcomeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text) ||
            declaredBranchOutcomes.Count == 0)
        {
            return false;
        }

        var candidates = new List<string>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            var heading = NormalizeOutcomeMarkdownMetadataLine(lines[index]);
            if (!IsBranchOutcomeDecisionHeading(heading))
            {
                continue;
            }

            for (var candidateIndex = index + 1; candidateIndex < lines.Length; candidateIndex++)
            {
                var candidateLine = lines[candidateIndex];
                if (LooksLikeMarkdownHeading(candidateLine))
                {
                    break;
                }

                var candidate = NormalizeOutcomeMarkdownMetadataLine(candidateLine);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var matches = declaredBranchOutcomes
            .Where(outcome => candidates.Any(candidate =>
                string.Equals(NormalizeBranchOutcomeKeyCandidate(candidate), outcome.Key, StringComparison.OrdinalIgnoreCase) ||
                ContainsBranchOutcomeKey(candidate, outcome.Key) ||
                ContainsBranchOutcomeTitle(candidate, outcome.Title)))
            .Select(outcome => outcome.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        branchOutcomeKey = matches[0];
        return true;
    }

    internal static bool IsBranchOutcomeDecisionHeading(string value)
        => value switch
        {
            _ when string.Equals(value, "Branch outcome key", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "Branch outcome", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "Validation decision", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "Repair decision", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "Acceptance decision", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "Outcome", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };

    internal static bool LooksLikeMarkdownHeading(string value)
        => value.TrimStart().StartsWith('#');

    internal static IEnumerable<string> ReadExplicitBranchOutcomeKeys(string text)
    {
        foreach (Match match in ExplicitBranchOutcomeKeyLineRegex().Matches(text))
        {
            var value = match.Groups["key"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            var line = NormalizeOutcomeMarkdownMetadataLine(lines[index]);
            if (!string.Equals(line, "Branch outcome key", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = NormalizeBranchOutcomeKeyCandidate(lines[index + 1]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    internal static string NormalizeOutcomeMarkdownMetadataLine(string value)
        => value.Trim().TrimStart('#', '-', '*', ' ').Trim(' ', '*', '`', ':');

    internal static string NormalizeBranchOutcomeKeyCandidate(string value)
    {
        var trimmed = NormalizeOutcomeMarkdownMetadataLine(value).Trim('.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return Regex.IsMatch(
            trimmed,
            @"^[A-Za-z0-9][A-Za-z0-9._-]*$",
            RegexOptions.CultureInvariant)
            ? trimmed
            : string.Empty;
    }

    internal static bool LooksLikeBranchSelectionText(string text)
        => ContainsAny(
            text,
            "branch outcome",
            "branch key",
            "selected branch",
            "select branch",
            "selected outcome",
            "select outcome",
            "choose outcome",
            "chose outcome",
            "validation decision",
            "repair decision",
            "acceptance decision",
            "selected decision",
            "route to",
            "routing to",
            "# outcome",
            "outcome -",
            "outcome:");

    internal static IEnumerable<string> EnumerateDeclaredBranchOutcomeKeys(string prompt)
        => EnumerateDeclaredBranchOutcomes(prompt).Select(outcome => outcome.Key);

    internal static IEnumerable<BranchOutcomePromptDescriptor> EnumerateDeclaredBranchOutcomes(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        foreach (var line in prompt.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryReadDeclaredBranchOutcomeLine(line, out var key, out var rest))
            {
                yield return new BranchOutcomePromptDescriptor(
                    key,
                    ExtractBranchOutcomeTitle(rest));
            }
        }
    }

    internal static bool TryReadDeclaredBranchOutcomeLine(
        string line,
        out string key,
        out string rest)
    {
        key = string.Empty;
        rest = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] != '-')
        {
            return false;
        }

        var body = trimmed[1..].TrimStart();
        var separatorIndex = body.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = NormalizeBranchOutcomeKeyCandidate(body[..separatorIndex]);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        rest = body[(separatorIndex + 1)..].Trim();
        return true;
    }

    internal static string ExtractBranchOutcomeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var title = value.Trim();
        var separatorIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            title = title[..separatorIndex];
        }

        return title.Trim(' ', '`', '*', '.', ':', ';', '-');
    }

    internal static bool ContainsBranchOutcomeKey(string text, string branchOutcomeKey)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(branchOutcomeKey))
        {
            return false;
        }

        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9._-]){Regex.Escape(branchOutcomeKey.Trim())}(?![A-Za-z0-9._-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool ContainsBranchOutcomeTitle(string text, string branchOutcomeTitle)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !IsInferableBranchOutcomeTitle(branchOutcomeTitle))
        {
            return false;
        }

        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9]){Regex.Escape(branchOutcomeTitle.Trim())}(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool IsInferableBranchOutcomeTitle(string branchOutcomeTitle)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeTitle))
        {
            return false;
        }

        var words = Regex.Matches(branchOutcomeTitle, @"[A-Za-z0-9]+", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToArray();
        return words.Length >= 2 && words.Sum(word => word.Length) >= 8;
    }

    internal sealed record BranchOutcomePromptDescriptor(string Key, string Title);
}
