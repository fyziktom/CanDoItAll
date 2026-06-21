using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ArtifactEvidence;

public sealed record ArtifactEvidenceVerificationRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<ProcessArtifactProjectionLineageDescriptor> ProjectionLineage,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> ProjectionSourceOrder,
    IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor> ProviderNativeBrowserEvidence,
    IReadOnlyList<ProcessArtifactValidationRequirementDescriptor> ValidationRequirements,
    IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    IReadOnlyList<ProcessArtifactRecordSnapshot> ArtifactRecords,
    DateTimeOffset RequestedAt);
