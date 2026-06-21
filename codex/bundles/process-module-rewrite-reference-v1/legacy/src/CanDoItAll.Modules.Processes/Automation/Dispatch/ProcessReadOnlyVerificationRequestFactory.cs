using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessReadOnlyVerificationRequestFactory
{
    public static ProcessDriverVerificationRequest Create(
        ProcessDriverPermissionMode permissionMode,
        ProcessDriverCapabilityScope scope,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        string callerContext)
    {
        return new ProcessDriverVerificationRequest(
            permissionMode,
            scope,
            evidenceReferences,
            requestedOperations,
            callerContext.Trim(),
            ProcessDriverContractVersion.Current);
    }
}
