using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedProcessArtifactOutcomeReaderTests
{
    [Fact]
    public void Read_uses_canonical_header_without_treating_nested_evidence_as_step_metadata()
    {
        const string content = """
            # Feature repair outcome

            Status: Completed
            Branch outcome key: feature-repair-applied

            ## Upstream evidence excerpt

            Status: Completed
            Branch outcome key: feature-repair-required
            """;

        var result = ManagedProcessArtifactOutcomeReader.Read(content);

        Assert.True(result.IsValid, result.FailureMessage);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Status);
        Assert.Equal("feature-repair-applied", result.BranchOutcomeKey);
    }

    [Fact]
    public void Read_rejects_conflicting_values_inside_canonical_header()
    {
        const string content = """
            Status: Completed
            Branch outcome key: feature-repair-applied
            Branch outcome key: feature-repair-required

            ## Repair evidence
            """;

        var result = ManagedProcessArtifactOutcomeReader.Read(content);

        Assert.False(result.IsValid);
        Assert.Contains("canonical header", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_preserves_legacy_heading_metadata_recovery()
    {
        const string content = """
            # Feature repair outcome

            ## Status

            Completed

            ## Branch Outcome

            - BranchOutcomeKey: feature-repair-applied
            """;

        var result = ManagedProcessArtifactOutcomeReader.Read(content);

        Assert.True(result.IsValid, result.FailureMessage);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Status);
        Assert.Equal("feature-repair-applied", result.BranchOutcomeKey);
    }
}
