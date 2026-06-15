namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public sealed record SoftwareDeliveryImplementationContractSnapshot(
    string ContractText,
    string TriggerText,
    string AdditionalGroundingText,
    bool RequiresConcreteImplementationProof,
    bool RequiresConcreteImplementationReview,
    bool RequiresConcreteBrowserProof,
    bool UsesScaffoldContractDrivenSetup,
    bool IsDotNetSolutionSetupScaffoldMutationStep);

public sealed record SoftwareDeliveryToolReceiptSnapshot(
    string ToolName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Succeeded,
    string RequestSummary,
    string WorkingDirectory,
    string ExitSummary,
    IReadOnlyList<string> WorkspacePaths,
    IReadOnlyList<string> OutputFiles);

public sealed record SoftwareDeliveryArtifactExpectationSnapshot(
    string Id,
    string Title,
    bool IsRequired,
    string ValidationRequirementSummary,
    string ExpectedPath,
    string ArtifactKind);

public sealed record SoftwareDeliveryArtifactRecordSnapshot(
    string Id,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record SoftwareDeliveryExternalTargetSnapshot(
    IReadOnlyList<string> AllowedAliases,
    string GroundedMappedAlias,
    string GroundedAbsolutePath,
    bool HasGroundedTarget,
    bool HasScaffoldTarget,
    string CurrentRunManagedArtifactRoot,
    string CurrentRunManagedOutputRoot);

public sealed record SoftwareDeliveryBrowserEvidenceSnapshot(
    bool BrowserProofRequired,
    bool HasCurrentRunBrowserEvidence,
    bool HasConsoleErrorEvidence,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> ArtifactPaths,
    string Summary);

public sealed record SoftwareDeliveryCarriedProofSnapshot(
    bool HasCarriedConcreteImplementationProof,
    bool HasCarriedRunnableApplicationProof,
    bool HasCarriedConcreteProductMutation,
    string SourceRunId,
    string Summary);

public sealed record SoftwareDeliveryRunnableHostSnapshot(
    IReadOnlyList<string> RunnableProjectPaths,
    string InvalidHostSummary);

public sealed record SoftwareDeliveryPathFacts(
    IReadOnlyList<string> WorkspacePaths,
    IReadOnlyList<string> OutputFiles,
    IReadOnlyList<string> ExpectedArtifactPaths,
    IReadOnlyList<string> ManagedArtifactRoots,
    IReadOnlyList<string> ManagedOutputRoots);

public enum SoftwareDeliveryImplementationStack
{
    Unknown = 0,
    DotNet = 1,
    JavaScript = 2,
    Mixed = 3,
    NonSoftware = 4
}

public sealed record SoftwareDeliveryHistoricalExecutionProofSnapshot(
    bool IsCarryForwardEligible,
    bool HasSuccessfulConcreteProductMutation);
