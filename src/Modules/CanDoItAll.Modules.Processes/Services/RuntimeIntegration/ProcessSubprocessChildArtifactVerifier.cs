using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessSubprocessVerifiedChildArtifact(
    string ArtifactRef,
    string StepKey,
    string ArtifactExpectationKey,
    string ContentHash,
    string Content);

internal sealed class ProcessSubprocessChildArtifactVerifier(IWorkspaceFileService workspaceFiles)
{
    internal bool CanBridge(
        string candidateRef,
        string childStepKey,
        string childArtifactExpectationKey,
        string requiredBranchOutcomeKey,
        string expectedContentHash,
        out ProcessSubprocessVerifiedChildArtifact verifiedArtifact)
    {
        verifiedArtifact = null!;
        var stat = workspaceFiles.StatPath(candidateRef);
        if (!stat.Succeeded ||
            !stat.Exists ||
            !string.Equals(stat.PathKind, "file", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(expectedContentHash))
        {
            return false;
        }

        var readResult = workspaceFiles.ReadTextFile(
            candidateRef,
            WorkspaceFileLimits.MaxTextReadCharacters);
        if (!readResult.Succeeded ||
            readResult.IsTruncated ||
            !string.Equals(
                ComputeContentHash(readResult.Content),
                expectedContentHash.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!readResult.Content.Contains(
                ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading,
                StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(requiredBranchOutcomeKey) &&
             !DeclaresExactlyOneBranchOutcome(readResult.Content, requiredBranchOutcomeKey)))
        {
            return false;
        }

        verifiedArtifact = new ProcessSubprocessVerifiedChildArtifact(
            candidateRef,
            childStepKey,
            childArtifactExpectationKey,
            expectedContentHash.Trim(),
            readResult.Content);
        return true;
    }

    private static string ComputeContentHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool DeclaresExactlyOneBranchOutcome(
        string content,
        string requiredBranchOutcomeKey)
    {
        var declaredKeys = ProcessManagedArtifactBranchOutcomeReader.ReadKeys(content)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return declaredKeys.Length == 1 &&
               string.Equals(
                   declaredKeys[0],
                   requiredBranchOutcomeKey,
                   StringComparison.OrdinalIgnoreCase);
    }
}
