namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static class ProcessTargetGroundingLedgerBuilder
    {
        internal static IReadOnlyList<ProcessTargetGroundingRecord> ResolveExternalTargetGroundings(
            DispatchCandidate candidate,
            string? projectStructureGroundingSummary,
            string? artifactInspectionGroundingSummary)
            => ProcessRunAutomationDispatchService.ResolveExternalTargetGroundings(
                candidate,
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary);

        internal static IReadOnlyList<object> BuildGroundedTargetAliasLedger(
            IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings,
            IReadOnlyList<string> writableAliases)
            => ProcessRunAutomationDispatchService.BuildGroundedTargetAliasLedger(targetGroundings, writableAliases);

        internal static IReadOnlyList<string> ResolveMutableExternalTargetAliases(
            DispatchCandidate candidate,
            IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings)
            => ProcessRunAutomationDispatchService.ResolveMutableExternalTargetAliases(candidate, targetGroundings);

        internal static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
            DispatchCandidate candidate,
            IReadOnlyList<ProcessTargetGroundingRecord> targetGroundings,
            IReadOnlyList<string> allowedExternalTargetAliases,
            bool allowExternalTargetMutation,
            ProcessStepOperationContract operationContract)
            => ProcessRunAutomationDispatchService.ResolveReadOnlyExternalTargetAliases(
                candidate,
                targetGroundings,
                allowedExternalTargetAliases,
                allowExternalTargetMutation,
                operationContract);

        internal static IReadOnlyList<string> PruneAllowedExternalTargetAliasesForCurrentRun(IEnumerable<string> aliases)
            => ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(aliases);
    }
}
