namespace CanDoItAll.Processes.Drivers.Abstractions.Evidence;

public sealed record ProcessDriverEvidenceReference(
    ProcessDriverEvidenceReferenceKind Kind,
    string Uri,
    string ContentHash,
    ProcessDriverCoreDescriptorFamily? CoreDescriptorFamily);

public enum ProcessDriverEvidenceReferenceKind
{
    BundleProofArtifact = 1,
    CommandTranscript = 2,
    TestResult = 3,
    SourceAssertion = 4,
    CoreDescriptor = 5,
    OfficeReadonlyArtifact = 6,
    BusinessReadonlyArtifact = 7
}

public enum ProcessDriverCoreDescriptorFamily
{
    ExecutionEvidence = 1,
    FinalizerEvidence = 2,
    RetryDiagnostics = 3,
    ArtifactProjectionEvidence = 4,
    ArtifactProjectionValidation = 5
}
