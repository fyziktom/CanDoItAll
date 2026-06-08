using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Processes.Drivers.Abstractions.Gateway;

public static class ProcessDriverVerificationGatewayLaneRules
{
    private static readonly IReadOnlyList<ProcessDriverOperation> SuppliedEvidenceOperations =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics,
        ProcessDriverOperation.ExplainDenial
    ];

    private static readonly IReadOnlyList<ProcessDriverOperation> RuntimeEvidenceOperations =
    [
        ProcessDriverOperation.ReadProcessFacts,
        ProcessDriverOperation.ReturnDiagnostics,
        ProcessDriverOperation.ExplainDenial
    ];

    public static IReadOnlyList<ProcessDriverVerificationGatewayLaneDescriptor> AllowedLanes { get; } =
    [
        Describe(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification),
        Describe(ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency),
        Describe(ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency),
        Describe(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead),
        Describe(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead)
    ];

    public static ProcessDriverVerificationGatewayLaneDescriptor Describe(ProcessDriverVerificationGatewayLane lane)
    {
        return lane switch
        {
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification => new ProcessDriverVerificationGatewayLaneDescriptor(
                lane,
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                ProcessDriverPermissionMode.VerificationOnly,
                ProcessDriverEvidenceReferenceKind.CommandTranscript,
                ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
                SuppliedEvidenceOperations),
            ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency => new ProcessDriverVerificationGatewayLaneDescriptor(
                lane,
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                ProcessDriverPermissionMode.ManagerReadonly,
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
                RuntimeEvidenceOperations),
            ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency => new ProcessDriverVerificationGatewayLaneDescriptor(
                lane,
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly,
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence,
                SuppliedEvidenceOperations),
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead => new ProcessDriverVerificationGatewayLaneDescriptor(
                lane,
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                ProcessDriverPermissionMode.VerificationOnly,
                ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
                null,
                SuppliedEvidenceOperations),
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead => new ProcessDriverVerificationGatewayLaneDescriptor(
                lane,
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                ProcessDriverPermissionMode.VerificationOnly,
                ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
                null,
                SuppliedEvidenceOperations),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported verification gateway lane.")
        };
    }
}
