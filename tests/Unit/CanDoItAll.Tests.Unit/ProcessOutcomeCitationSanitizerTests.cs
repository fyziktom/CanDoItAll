using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessOutcomeCitationSanitizerTests
{
    [Fact]
    public void RemoveNonCitableSourceMetadataFromOutcome_strips_non_citable_metadata_from_every_output_field()
    {
        const string primaryArtifactRef =
            "artifacts/process-runs/7063dafe-2b01-44a4-a016-381b3741d0e0/steps/quality-check.md";
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Validated the product.\nSourceDocName: managed-files/project-media/brief.md",
            BranchOutcomeKey = "quality-accepted",
            BranchOutcomeTitle = "Quality accepted",
            EvidenceRefs =
            [
                primaryArtifactRef,
                "SourceDocLink: managed-files/project-media/brief.md"
            ],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-001",
                    Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                    Summary = "The required behavior was verified.",
                    EvidenceRefs =
                    [
                        primaryArtifactRef,
                        "managed-files/project-media/brief.md"
                    ]
                }
            ],
            NextActions =
            [
                "Archive the managed artifact.",
                "SourceDocName: managed-files/project-media/brief.md"
            ],
            HumanReadableSummaryMarkdown = "Validation completed.\nSourceDocLink: managed-files/project-media/brief.md"
        };

        var sanitized = ProcessOutcomeCitationSanitizer.RemoveNonCitableSourceMetadataFromOutcome(output);

        Assert.Equal("Validated the product.", sanitized.Reason);
        Assert.Equal("quality-accepted", sanitized.BranchOutcomeKey);
        Assert.Equal("Quality accepted", sanitized.BranchOutcomeTitle);
        Assert.Equal([primaryArtifactRef], sanitized.EvidenceRefs);
        Assert.Equal(["Archive the managed artifact."], sanitized.NextActions);
        Assert.Equal("Validation completed.", sanitized.HumanReadableSummaryMarkdown);

        var acceptanceEvidence = Assert.Single(sanitized.AcceptanceCriteriaEvidence);
        Assert.Equal("AC-001", acceptanceEvidence.CriterionId);
        Assert.Equal(ProcessAcceptanceCriterionEvidenceStatus.Passed, acceptanceEvidence.Status);
        Assert.Equal("The required behavior was verified.", acceptanceEvidence.Summary);
        Assert.Equal([primaryArtifactRef], acceptanceEvidence.EvidenceRefs);
    }

    [Fact]
    public void RemoveNonCitableEvidenceRef_preserves_managed_process_artifact_reference()
    {
        const string managedArtifactRef =
            "artifacts/process-runs/7063dafe-2b01-44a4-a016-381b3741d0e0/steps/quality-check.md";

        var sanitized = ProcessOutcomeCitationSanitizer.RemoveNonCitableEvidenceRef(managedArtifactRef);

        Assert.Equal(managedArtifactRef, sanitized);
    }
}
