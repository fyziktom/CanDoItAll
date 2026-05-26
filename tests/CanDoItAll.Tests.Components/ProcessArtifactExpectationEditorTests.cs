using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessArtifactExpectationEditorTests
{
    [Fact]
    public void Render_SB01_INV_004_exposes_decision_record_and_approval_required_options()
    {
        using var context = new TestContext();
        var artifact = new ProcessArtifactExpectationEditorModel
        {
            Id = Guid.NewGuid(),
            Title = "Decision record",
            ArtifactKind = ProcessArtifactKind.Evidence,
            TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired
        };

        var cut = context.RenderComponent<ProcessArtifactExpectationEditor>(
            ComponentParameter.CreateParameter(nameof(ProcessArtifactExpectationEditor.Model), artifact));

        Assert.Contains("DecisionRecord", cut.Markup);
        Assert.Contains("ApprovalRequired", cut.Markup);

        var selects = cut.FindAll("select");
        selects[0].Change(ProcessArtifactKind.DecisionRecord.ToString());
        selects = cut.FindAll("select");
        selects[1].Change(ProcessArtifactTrustRequirement.ApprovalRequired.ToString());

        Assert.Equal(ProcessArtifactKind.DecisionRecord, artifact.ArtifactKind);
        Assert.Equal(ProcessArtifactTrustRequirement.ApprovalRequired, artifact.TrustRequirement);
    }
}
