using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Projects.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectDefinitionOriginalTests {
    [Fact]
    public void Acquired_original_is_a_separate_immutable_definition_not_the_editable_model() {
        var draft = new ProjectsScenarioStore(ProjectsScenario.Portfolio).Acquire(ProjectsScenarioStore.RootId);
        var original = Assert.IsType<ProjectDefinitionSnapshot>(draft.Original);
        var name = original.Scalars.Name;
        var goal = original.Phases[0].Goal;
        var option = Assert.Single(original.Options);

        draft.Model.Name = "Later unsaved name";
        draft.Model.Phases[0].Goal = "Later unsaved phase";
        draft.Model.Phases.RemoveAt(1);
        draft.Model.Options.Clear();

        Assert.Equal(name, original.Scalars.Name);
        Assert.Equal(goal, original.Phases[0].Goal);
        Assert.Equal(2, original.Phases.Count);
        Assert.Equal(option, Assert.Single(original.Options));
        Assert.Throws<NotSupportedException>(() => ((IList<ProjectDefinitionPhase>)original.Phases).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ProjectDefinitionOption>)original.Options).Clear());
        Assert.Equal(original.Expectation, draft.Model.ExpectedDefinition);
    }

    [Fact]
    public async Task Accepted_original_uses_exact_normalized_receipt_values_while_later_live_input_is_retained() {
        var store = new ProjectsScenarioStore(ProjectsScenario.Portfolio);
        var draft = store.Acquire(ProjectsScenarioStore.RootId);
        draft.Model.Name = "  Submitted name  ";
        draft.Model.Phases[0].Goal = "  Submitted goal  ";
        var submitted = draft.Capture();
        var accepted = (await store.SaveAsync(submitted.Model)).Value!;
        draft.Model.Name = "Later local name";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        draft.Model.Phases[0].Goal = "Later local phase";
        draft.EditContext.NotifyFieldChanged(new(draft.Model.Phases[0], nameof(ProjectPhaseEditorModel.Goal)));

        draft.Acknowledge(submitted, accepted);

        Assert.Equal(accepted.Definition, draft.Original!.Expectation);
        Assert.Equal(accepted.Name, draft.Original.Scalars.Name);
        Assert.Equal(accepted.Phases.OrderBy(row => row.SubmittedIndex).Select(row => row.Id), draft.Original.Phases.Select(row => row.Id));
        Assert.Equal(accepted.Phases[0].Goal, draft.Original.Phases[0].Goal);
        Assert.Equal(accepted.Options.Count, draft.Original.Options.Count);
        Assert.Equal("Later local name", draft.Model.Name);
        Assert.Equal("Later local phase", draft.Model.Phases[0].Goal);
    }

    [Fact]
    public void An_original_for_another_revision_cannot_be_attached_to_an_acquired_model() {
        var acquired = new ProjectsScenarioStore(ProjectsScenario.Portfolio).Acquire(ProjectsScenarioStore.RootId);
        var original = acquired.Original!;
        var unrelated = new ProjectDefinitionSnapshot(new(original.Expectation.Project, Guid.NewGuid()),
            original.Scalars, original.ExternalIdentity, original.Phases, original.Options);

        Assert.Throws<ArgumentException>(() => new ProjectEditorDraft(acquired.Model, original: unrelated));
    }
}
