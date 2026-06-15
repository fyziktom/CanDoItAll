namespace CanDoItAll.Modules.Processes;

internal static class ProcessExternalTargetReferenceGuard
{
    public static string ResolveOutOfScopeReferenceSummary(
        string? text,
        IReadOnlyList<string> allowedAliases)
    {
        return ProcessExternalTargetGroundingService.InspectReferences(text, allowedAliases).Summary;
    }
}

