using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessChildArtifactVerifier(IWorkspaceFileService workspaceFiles)
{
    internal bool CanBridge(string candidateRef, string requiredBranchOutcomeKey)
    {
        var stat = workspaceFiles.StatPath(candidateRef);
        if (!stat.Exists)
        {
            return string.IsNullOrWhiteSpace(requiredBranchOutcomeKey);
        }

        var readResult = workspaceFiles.ReadTextFile(candidateRef, maxCharacters: 200000);
        if (!readResult.Succeeded)
        {
            return false;
        }

        var isAccepted = !readResult.Content.Contains(
                             ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading,
                             StringComparison.Ordinal) ||
                         readResult.Content.Contains(
                             ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading,
                             StringComparison.Ordinal);
        return isAccepted &&
               (string.IsNullOrWhiteSpace(requiredBranchOutcomeKey) ||
                DeclaresBranchOutcome(readResult.Content, requiredBranchOutcomeKey));
    }

    private static bool DeclaresBranchOutcome(string content, string requiredBranchOutcomeKey)
    {
        if (ProcessBranchOutcomeResolver.ReadExplicitBranchOutcomeKeys(content)
            .Contains(requiredBranchOutcomeKey, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        const string branchHeading = "### Branch Outcome";
        var branchStart = content.IndexOf(branchHeading, StringComparison.OrdinalIgnoreCase);
        if (branchStart < 0)
        {
            return false;
        }

        var branchEnd = content.IndexOf("### Summary", branchStart + branchHeading.Length, StringComparison.OrdinalIgnoreCase);
        var branchSection = branchEnd < 0
            ? content[branchStart..]
            : content[branchStart..branchEnd];
        return Regex.IsMatch(
            branchSection,
            $@"(?im)^\s*-\s*Key:\s*{Regex.Escape(requiredBranchOutcomeKey)}\s*$",
            RegexOptions.CultureInvariant);
    }
}
