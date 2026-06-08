using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessReadOnlyVerificationOperationPolicy
{
    public static IReadOnlyList<ProcessDriverOperation> TranscriptVerificationDefaults { get; } =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics,
        ProcessDriverOperation.ReadProcessFacts
    ];

    public static IReadOnlyList<ProcessDriverOperation> RuntimeEvidenceDefaults { get; } =
    [
        ProcessDriverOperation.ReadProcessFacts,
        ProcessDriverOperation.ReturnDiagnostics
    ];

    public static IReadOnlyList<ProcessDriverOperation> Normalize(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations,
        IReadOnlyList<ProcessDriverOperation> defaultOperations)
    {
        var normalized = (requestedOperations ?? [])
            .Distinct()
            .ToArray();

        return normalized.Length > 0 ? normalized : defaultOperations;
    }
}
