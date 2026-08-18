using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.Processes;

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

    [Fact]
    public void RemoveNonCitableSourceMetadataFromOutcome_preserves_runtime_owned_envelopes_verbatim()
    {
        var childArtifact = new ProcessSubprocessVerifiedChildArtifact(
            "artifacts/process-runs/7063dafe-2b01-44a4-a016-381b3741d0e0/steps/feature-handoff.md",
            "feature-handoff",
            "feature-handoff-packet",
            "sha256:original-child-content",
            """
            ## Runtime Accepted Completion Gates
            Status: Completed
            SourceDocLink: managed-files/project-media/child-proof.md
            Product note: tool-runs/child-output.json
            """);
        var envelope = ParentSubprocessVerifiedChildOutputEnvelope.Format(childArtifact);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "SourceDocName: managed-files/project-media/outside.md\nAccepted child output.",
            EvidenceRefs = [],
            NextActions = [],
            HumanReadableSummaryMarkdown =
                $"SourceDocLink: managed-files/project-media/outside.md{Environment.NewLine}{envelope}"
        };

        var sanitized = ProcessOutcomeCitationSanitizer.RemoveNonCitableSourceMetadataFromOutcome(output);

        Assert.Equal("Accepted child output.", sanitized.Reason);
        Assert.Equal(envelope, sanitized.HumanReadableSummaryMarkdown);
        Assert.Contains(
            "SourceDocLink: managed-files/project-media/child-proof.md",
            sanitized.HumanReadableSummaryMarkdown,
            StringComparison.Ordinal);
        Assert.Contains(
            "Product note: tool-runs/child-output.json",
            sanitized.HumanReadableSummaryMarkdown,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryRemoveNonCitableSourceMetadataLines_does_not_rewrite_malformed_runtime_envelopes()
    {
        var content =
            $"SourceDocLink: managed-files/project-media/outside.md{Environment.NewLine}" +
            ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker;

        var changed = ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(
            content,
            out var unchangedContent);

        Assert.False(changed);
        Assert.Equal(content, unchangedContent);
    }

    [Fact]
    public void TryRemoveNonCitableSourceMetadataLines_preserves_cross_family_nested_envelopes_verbatim()
    {
        var content = string.Join(
            Environment.NewLine,
            ParentSubprocessForwardedContextEnvelope.BeginMarker,
            ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
            "SourceDocLink: managed-files/project-media/child-proof.md",
            ParentSubprocessVerifiedChildOutputEnvelope.EndMarker,
            ParentSubprocessForwardedContextEnvelope.EndMarker);

        var changed = ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(
            content,
            out var unchangedContent);

        Assert.False(changed);
        Assert.Equal(content, unchangedContent);
    }

    [Fact]
    public void TryRemoveNonCitableSourceMetadataLines_preserves_spacing_when_no_metadata_is_removed()
    {
        var content = string.Join(
            Environment.NewLine,
            "Accepted managed artifact.",
            string.Empty,
            string.Empty,
            "---",
            "Runtime acceptance proof.");

        var changed = ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(
            content,
            out var unchangedContent);

        Assert.False(changed);
        Assert.Equal(content, unchangedContent);
    }

    [Fact]
    public void TryRemoveNonCitableSourceMetadataLines_compacts_spacing_after_metadata_is_removed()
    {
        var content = string.Join(
            Environment.NewLine,
            "Accepted managed artifact.",
            "SourceDocLink: managed-files/project-media/internal.md",
            string.Empty,
            string.Empty,
            "---",
            "Runtime acceptance proof.");
        var expected = string.Join(
            Environment.NewLine,
            "Accepted managed artifact.",
            string.Empty,
            "---",
            "Runtime acceptance proof.");

        var changed = ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(
            content,
            out var sanitizedContent);

        Assert.True(changed);
        Assert.Equal(expected, sanitizedContent);
    }
}
