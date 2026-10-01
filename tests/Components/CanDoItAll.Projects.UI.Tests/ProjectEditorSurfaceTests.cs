using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Projects.UiSandbox;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectEditorSurfaceTests {
    [Fact]
    public async Task Submit_snapshots_before_held_browser_validation_and_retired_forms_cannot_dispatch() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var inspection = context.JSInterop.SetupModule("./_content/CanDoItAll.Projects.UI/project-editor.js")
            .Setup<ProjectFormValidity>("inspect", _ => true);
        context.Services.AddCanDoItAllBaseLib();
        var draft = NewDraft();
        ProjectEditorSubmission? received = null;
        var cut = Render(context, draft);
        cut.Render(p => p.Add(x => x.Save, submitted => received = submitted));
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Single(inspection.Invocations));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Later edit" }));
        inspection.SetResult(new(true, null, null));
        await pending;
        Assert.NotNull(received);
        Assert.Equal("Project", received.Model.Name);
        Assert.Equal("Later edit", draft.Model.Name);
        Assert.True(draft.EditRevision > received.EditRevision);

        var retired = Render(context, NewDraft());
        int retiredCalls = 0;
        retired.Render(p => p.Add(x => x.Save, _ => retiredCalls++));
        await retired.Instance.DisposeAsync();
        await retired.InvokeAsync(() => retired.Find("form").SubmitAsync());
        Assert.Equal(0, retiredCalls);
    }

    [Fact]
    public async Task Raw_invalid_date_and_context_survive_every_step_and_overview() {
        using var context = Context();
        var draft = NewDraft();
        var cut = Render(context, draft);
        var date = cut.FindComponents<InputDate<DateTime?>>().First();
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-target-date']").ChangeAsync(new() { Value = "not-a-date" }));
        foreach (int step in Enumerable.Range(0, 5)) {
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.WizardStep, step)));
            Assert.Same(date.Instance, cut.FindComponents<InputDate<DateTime?>>().First().Instance);
            Assert.Same(draft.EditContext, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.NotEmpty(draft.EditContext.GetValidationMessages(new FieldIdentifier(draft.Model, nameof(draft.Model.TargetDateUtc))));
            Assert.Equal("not-a-date", cut.Find("[data-testid='project-target-date']").GetAttribute("value"));
        }
        await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.IsEditorMode, false)));
        await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.IsEditorMode, true)));
        Assert.Same(date.Instance, cut.FindComponents<InputDate<DateTime?>>().First().Instance);
        Assert.False(await cut.InvokeAsync(draft.Validate));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-target-date']").ChangeAsync(new() { Value = "2026-12-07" }));
        Assert.True(await cut.InvokeAsync(draft.Validate));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_save_actions_validate_the_same_context(bool alternate) {
        using var context = Context();
        var draft = NewDraft();
        draft.Model.Name = string.Empty;
        int calls = 0;
        var cut = Render(context, draft, () => calls++);
        string button = alternate ? "project-save-open-button" : "project-save-button";
        await cut.InvokeAsync(() => cut.Find($"[data-testid='{button}']").ClickAsync(new()));
        Assert.Equal(0, calls);
        Assert.Contains("Project name is required.", cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Žluťoučký 東京" }));
        await cut.InvokeAsync(() => cut.Find($"[data-testid='{button}']").ClickAsync(new()));
        Assert.Equal(1, calls);
        Assert.Equal("Žluťoučký 東京", draft.Model.Name);
    }

    [Fact]
    public async Task Owner_ack_keeps_later_edits_and_maps_ids_to_original_rows_after_reorder_and_replacement() {
        using var context = Context();
        var draft = NewDraft();
        draft.Model.Phases = [new() { Name = "first " }, new() { Name = "second " }];
        var first = draft.Model.Phases[0];
        var second = draft.Model.Phases[1];
        var option = draft.Model.Options[0];
        option.OptionName = "C# ";
        var cut = Render(context, draft);
        var submitted = draft.Capture();
        var result = await new ProjectsScenarioStore(ProjectsScenario.Empty).SaveAsync(submitted.Model);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Later name" }));
        draft.Model.Phases.Remove(first);
        var replacement = new ProjectPhaseEditorModel { Name = "replacement" };
        draft.Model.Phases.Insert(0, replacement);
        second.Goal = "Later goal";
        draft.EditContext.NotifyFieldChanged(new(second, nameof(second.Goal)));
        option.Notes = "Later option notes";
        draft.EditContext.NotifyFieldChanged(new(option, nameof(option.Notes)));
        draft.Acknowledge(submitted, result.Value!);
        Assert.Equal("Later name", draft.Model.Name);
        Assert.Equal("Later goal", second.Goal);
        Assert.Equal("Later option notes", option.Notes);
        Assert.Null(replacement.Id);
        Assert.Null(first.Id);
        Assert.Equal(result.Value!.Phases[1].Id, second.Id);
        Assert.Equal(result.Value.Options[0].Id, option.Id);
        Assert.Equal("second", second.Name);
        Assert.Equal("C#", option.OptionName);
        Assert.Same(draft.EditContext, cut.FindComponent<EditForm>().Instance.EditContext);
        var repeated = draft.Capture();
        var secondResult = await new ProjectsScenarioStore(ProjectsScenario.Empty).SaveAsync(repeated.Model);
        Assert.Equal(second.Id, secondResult.Value!.Phases[1].Id);
        Assert.Equal(option.Id, secondResult.Value.Options[0].Id);
    }

    [Fact]
    public async Task Typing_back_to_the_submitted_value_is_still_a_newer_field_revision() {
        using var context = Context();
        var draft = NewDraft();
        draft.Model.Name = "  Original  ";
        var cut = Render(context, draft);
        var submission = draft.Capture();
        var acknowledgement = await new ProjectsScenarioStore(ProjectsScenario.Empty).SaveAsync(submission.Model);
        foreach (string name in new[] { "Changed", "  Original  " }) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = name }));
        }
        draft.Acknowledge(submission, acknowledgement.Value!);
        Assert.Equal("  Original  ", draft.Model.Name);
    }

    [Fact]
    public void Acknowledged_seed_batch_does_not_clear_new_plans_or_replay_edited_original_plans() {
        var draft = NewDraft();
        var original = new StarterObjectDraft { Title = "Submitted" };
        draft.StarterObjects.Add(original);
        var submission = draft.Capture();
        original.Title = "Later planning edit";
        var later = new StarterObjectDraft { Title = "New plan" };
        draft.StarterObjects.Add(later);
        draft.AcknowledgeSeeds(submission);
        Assert.True(original.IsSeeded);
        Assert.Equal([original, later], draft.StarterObjects);
        Assert.Same(later, Assert.Single(draft.Capture().Seeds).Row);
    }

    [Fact]
    public async Task Foreign_lifetime_ack_is_rejected_before_any_identity_is_adopted() {
        var store = new ProjectsScenarioStore(ProjectsScenario.Portfolio);
        var draft = store.Acquire(ProjectsScenarioStore.RootId);
        var submission = draft.Capture();
        var acknowledgement = (await store.SaveAsync(submission.Model)).Value!;
        var wrong = acknowledgement with { Project = new(ProjectsScenarioStore.ProfileId, ProjectsScenarioStore.RootId, Guid.NewGuid()) };
        Assert.Throws<InvalidOperationException>(() => draft.Acknowledge(submission, wrong));
        Assert.Null(draft.Acknowledgement);
        Assert.Equal(ProjectsScenarioStore.RootId, draft.Model.ExpectedLifetimeId);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./_content/CanDoItAll.Projects.UI/project-editor.js")
            .Setup<ProjectFormValidity>("inspect", _ => true).SetResult(new(true, null, null));
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    internal static ProjectEditorDraft NewDraft() => new(new() {
        Name = "Project",
        Options = [new() { Category = ProjectOptionCategory.Language }]
    });

    internal static IRenderedComponent<ProjectModalHost> Render(BunitContext context, ProjectEditorDraft draft, Action? save = null)
        => context.Render<ProjectModalHost>(p => p.Add(x => x.Draft, draft).Add(x => x.IsOpen, true).Add(x => x.IsEditorMode, true)
            .Add(x => x.WizardSteps, ["Identity", "Dates and phases", "Stack profile", "Linked objects", "Review"])
            .Add(x => x.WizardTabs, Enumerable.Range(0, 5).Select(index => new SecondaryTabItem(index.ToString(), index.ToString())).ToArray())
            .Add(x => x.StarterObjectKinds, [ProjectObjectType.Note]).Add(x => x.ProjectSummaries, [])
            .Add(x => x.Save, save ?? (() => { })).Add(x => x.SaveAndOpenStructure, save ?? (() => { })));
}
