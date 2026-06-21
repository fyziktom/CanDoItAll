using System.ComponentModel.DataAnnotations;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessVerificationRuntimeHostOptions
{
    public const string SectionName = "Processes:VerificationRuntimeHost";
    public const int MinimumPayloadItemsPerLane = 1;
    public const int DefaultMaxPayloadItemsPerLane = 50;
    public const int MaximumPayloadItemsPerLane = 500;
    public const int MinimumSuppliedEvidenceContentBytes = 1;
    public const int DefaultMaxSuppliedEvidenceContentBytes = (int)ProcessDriverSuppliedEvidenceContentRules.MaxSuppliedEvidenceContentBytes;

    public bool Enabled { get; set; } = true;

    public ProcessVerificationRuntimeHostLaneOptions Lanes { get; set; } = new();

    [Range(MinimumPayloadItemsPerLane, MaximumPayloadItemsPerLane)]
    public int MaxPayloadItemsPerLane { get; set; } = DefaultMaxPayloadItemsPerLane;

    [Range(MinimumSuppliedEvidenceContentBytes, DefaultMaxSuppliedEvidenceContentBytes)]
    public int MaxSuppliedEvidenceContentBytes { get; set; } = DefaultMaxSuppliedEvidenceContentBytes;

    public bool IsLaneEnabled(ProcessDriverVerificationGatewayLane lane)
    {
        return lane switch
        {
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification => Lanes.DotNetRustTranscriptVerification,
            ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency => Lanes.RuntimeEvidenceConsistency,
            ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency => Lanes.ArtifactEvidenceConsistency,
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead => Lanes.OfficeEvidenceRead,
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead => Lanes.BusinessAnalysisRead,
            _ => false
        };
    }
}

internal sealed class ProcessVerificationRuntimeHostLaneOptions
{
    public bool DotNetRustTranscriptVerification { get; set; } = true;

    public bool RuntimeEvidenceConsistency { get; set; } = true;

    public bool ArtifactEvidenceConsistency { get; set; } = true;

    public bool OfficeEvidenceRead { get; set; } = true;

    public bool BusinessAnalysisRead { get; set; } = true;
}
