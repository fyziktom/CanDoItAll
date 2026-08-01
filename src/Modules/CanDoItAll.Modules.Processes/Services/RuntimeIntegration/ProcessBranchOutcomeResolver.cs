using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessBranchOutcomeResolver
{
    internal static bool TryResolveExactConfiguredBranchOutcome(
        ProcessStepOutcomeResult output,
        ProcessStepExecutionContract stepContract,
        out BranchOutcomeId branchOutcomeId)
    {
        branchOutcomeId = default;
        if (string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            return false;
        }

        var matchingOutcomes = stepContract.ConfiguredBranchOutcomeIds
            .Where(outcomeId => string.Equals(
                outcomeId.Value,
                output.BranchOutcomeKey,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingOutcomes.Length != 1)
        {
            return false;
        }

        branchOutcomeId = matchingOutcomes[0];
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
}
