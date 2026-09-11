using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Components;

public sealed class AssignmentEditorAdmissionTests {
    [Fact]
    public async Task Open_conversion_editor_keeps_captured_project_when_parent_model_is_replaced() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var original = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var model = new CrmOpportunityConversionEditorModel {
            OpportunityId = Guid.NewGuid(), LinkExistingProject = true, ExistingProjectId = original.ProjectId,
            ExpectedProjectAdmission = original, ProjectName = "Original selection"
        };
        CrmOpportunityConversionEditorModel? submitted = null;
        var opportunity = Opportunity(model.OpportunityId);
        var cut = harness.Context.Render<OpportunityConversionDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Opportunity, opportunity)
            .Add(component => component.Model, model)
            .Add(component => component.Save, value => submitted = value));
        var replacement = new ProjectWriteAdmission(original.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        model.ExistingProjectId = replacement.ProjectId;
        model.ExpectedProjectAdmission = replacement;
        cut.Render(parameters => parameters.Add(component => component.Model, model));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save conversion", StringComparison.Ordinal)).Click();
        Assert.NotNull(submitted);
        Assert.Equal(original.ProjectId, submitted.ExistingProjectId);
        Assert.Equal(original, submitted.ExpectedProjectAdmission);
    }

    [Fact]
    public async Task Existing_legacy_selection_is_not_rebound_when_conversion_editor_is_opened() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var model = new CrmOpportunityConversionEditorModel {
            OpportunityId = Guid.NewGuid(), LinkExistingProject = true, ExistingProjectId = Guid.NewGuid()
        };
        CrmOpportunityConversionEditorModel? submitted = null;
        var cut = harness.Context.Render<OpportunityConversionDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Opportunity, Opportunity(model.OpportunityId))
            .Add(component => component.Model, model)
            .Add(component => component.Save, value => submitted = value));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save conversion", StringComparison.Ordinal)).Click();
        Assert.NotNull(submitted);
        Assert.Equal(model.ExistingProjectId, submitted.ExistingProjectId);
        Assert.Null(submitted.ExpectedProjectAdmission);
    }

    private static CrmOpportunityDetailModel Opportunity(Guid id) => new(
        id, Guid.NewGuid(), "Account", "Won opportunity", OpportunityStage.Won, string.Empty,
        default, Guid.NewGuid(), "Owner", null, string.Empty, "USD", 100m, 100, null,
        string.Empty, string.Empty, string.Empty, "Summary", string.Empty, null, string.Empty, [], [],
        new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
}
