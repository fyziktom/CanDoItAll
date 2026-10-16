using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Projects.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectEditorDraftTests {
    [Fact]
    public void Capture_preserves_original_expectation_identity_intent_and_values_when_live_draft_changes() {
        var draft = new ProjectsScenarioStore(ProjectsScenario.Portfolio).Acquire(ProjectsScenarioStore.RootId);
        var original = draft.Model.ExpectedDefinition!;
        draft.Model.ExternalNamespace = "original";
        draft.Model.ExternalKey = "key";
        var phase = draft.Model.Phases[0];
        var originalName = phase.Name;

        var submission = draft.Capture();
        draft.Model.ExpectedDefinition = new(original.Project, Guid.NewGuid());
        draft.Model.ExternalNamespace = "later";
        draft.Model.ExternalKey = "later-key";
        phase.Name = "Later phase";
        draft.Model.Phases.Clear();

        Assert.Equal(original, submission.Model.ExpectedDefinition);
        Assert.Equal("original", submission.Model.ExternalNamespace);
        Assert.Equal("key", submission.Model.ExternalKey);
        Assert.Equal(originalName, submission.Model.Phases[0].Name);
        Assert.Same(phase, submission.PhaseRows[0]);
    }

    [Fact]
    public void Conflict_comparison_preserves_the_original_session_and_recovery_requires_a_new_current_based_draft() {
        var draft = new ProjectsScenarioStore(ProjectsScenario.Portfolio).Acquire(ProjectsScenarioStore.RootId);
        var context = draft.EditContext;
        var original = draft.Model.ExpectedDefinition;
        draft.Model.Name = "My unsaved name";
        var submitted = draft.Capture();
        draft.RecordConflict(submitted);
        draft.BeginConflictRead();
        draft.FailConflictRead("Read failed");
        Assert.Same(submitted, draft.Conflict!.Submitted);
        Assert.Equal(ProjectEditorConflictReadState.Failed, draft.Conflict.ReadState);
        Assert.Throws<InvalidOperationException>(() => draft.StartFromCurrent(false));

        var current = CurrentDefinition(draft);
        draft.BeginConflictRead();
        draft.CompleteConflictRead(current);
        Assert.Same(context, draft.EditContext);
        Assert.Same(original, draft.Model.ExpectedDefinition);
        Assert.Equal("My unsaved name", draft.Model.Name);
        Assert.Same(submitted, draft.Conflict!.Submitted);
        Assert.Same(current, draft.Conflict.Current);
        Assert.Equal("Saved phase", Assert.Single(draft.ComparisonModel!.Phases).Name);
        Assert.False(draft.CanMutate);

        foreach (bool preserve in new[] { false, true }) {
            var replacement = draft.StartFromCurrent(preserve);
            Assert.NotSame(context, replacement.EditContext);
            Assert.Same(current.Expectation, replacement.Model.ExpectedDefinition);
            Assert.Equal("Saved name", replacement.Model.Name);
            Assert.Equal(current.Phases[0].Id, Assert.Single(replacement.Model.Phases).Id);
            Assert.Equal(current.Options[0].Id, replacement.Model.Options.Single(row => row.Category == ProjectOptionCategory.Language).Id);
            Assert.Equal(preserve ? draft : null, replacement.PreviousDraft);
            Assert.Null(replacement.Conflict);
            Assert.True(replacement.CanMutate);
        }
        Assert.Same(original, draft.Model.ExpectedDefinition);
        Assert.Equal("My unsaved name", draft.Model.Name);
    }

    internal static ProjectDefinitionSnapshot CurrentDefinition(ProjectEditorDraft draft) => new(
        new(draft.Model.ExpectedDefinition!.Project, Guid.NewGuid()),
        new("Saved name", "Saved description", "Saved objective", ProjectStatus.Active, "Saved phase", new DateTime(2027, 1, 8, 0, 0, 0, DateTimeKind.Utc)),
        new("saved-namespace", "saved-key"),
        [new(Guid.NewGuid(), "Saved phase", "Saved goal", ProjectPhaseStatus.Planned, null, null)],
        [new(Guid.NewGuid(), ProjectOptionCategory.Language, "Saved language", "Saved notes")]);

    [Fact]
    public async Task Malformed_acknowledgement_is_rejected_before_any_draft_state_changes() {
        var store = new ProjectsScenarioStore(ProjectsScenario.Portfolio);
        var draft = store.Acquire(ProjectsScenarioStore.RootId);
        var initial = draft.Capture();
        draft.Acknowledge(initial, (await store.SaveAsync(initial.Model)).Value!);
        draft.Model.Name = "  Unnormalized draft name  ";
        var submission = draft.Capture();
        var accepted = (await store.SaveAsync(submission.Model)).Value!;
        var originalAcknowledgement = draft.Acknowledgement;
        var originalDefinition = draft.Model.ExpectedDefinition;
        var originalName = draft.Model.Name;
        var originalPhaseIds = draft.Model.Phases.Select(row => row.Id).ToArray();
        var originalOptionIds = draft.Model.Options.Select(row => row.Id).ToArray();
        ProjectEditorAcknowledgement[] malformed = [
            accepted with { Phases = [accepted.Phases[0], accepted.Phases[1] with { SubmittedIndex = submission.PhaseRows.Count }] },
            accepted with { Phases = [accepted.Phases[0], accepted.Phases[1] with { SubmittedIndex = 0 }] },
            accepted with { Phases = [accepted.Phases[0], accepted.Phases[1] with { Id = Guid.Empty }] },
            accepted with { Phases = [accepted.Phases[0], accepted.Phases[1] with { Id = Guid.NewGuid() }] },
            accepted with { Phases = [accepted.Phases[0]] },
            accepted with { Options = [accepted.Options[0] with { SubmittedIndex = submission.OptionRows.Count }] },
            accepted with { Options = [accepted.Options[0], accepted.Options[0]] },
            accepted with { Options = [accepted.Options[0] with { Id = Guid.Empty }] },
            accepted with { Options = [accepted.Options[0] with { Id = Guid.NewGuid() }] },
            accepted with { Options = [] },
            accepted with { Definition = originalDefinition! }
        ];

        foreach (var acknowledgement in malformed) {
            draft.RecordAcceptedOutcome(submission, acknowledgement);
            Assert.Throws<InvalidOperationException>(() => draft.Acknowledge(submission, acknowledgement));
            Assert.Same(submission, draft.AcceptedOutcome!.Submitted);
            Assert.Same(acknowledgement, draft.AcceptedOutcome.Accepted);
            Assert.Same(originalAcknowledgement, draft.Acknowledgement);
            Assert.Same(originalDefinition, draft.Model.ExpectedDefinition);
            Assert.Equal(originalName, draft.Model.Name);
            Assert.Equal(originalPhaseIds, draft.Model.Phases.Select(row => row.Id));
            Assert.Equal(originalOptionIds, draft.Model.Options.Select(row => row.Id));
        }
    }

    [Theory]
    [InlineData(LaterOptionChange.None)]
    [InlineData(LaterOptionChange.Retyped)]
    [InlineData(LaterOptionChange.Removed)]
    public async Task Omitted_persisted_option_loses_its_old_identity_without_losing_later_intent(LaterOptionChange laterChange) {
        var store = new ProjectsScenarioStore(ProjectsScenario.Portfolio);
        var draft = store.Acquire(ProjectsScenarioStore.RootId);
        var initial = draft.Capture();
        draft.Acknowledge(initial, (await store.SaveAsync(initial.Model)).Value!);
        var option = draft.Model.Options.Single(row => row.Category == ProjectOptionCategory.Language);
        var originalId = Assert.IsType<Guid>(option.Id);
        option.OptionName = string.Empty;
        option.Notes = string.Empty;
        var submission = draft.Capture();
        var accepted = (await store.SaveAsync(submission.Model)).Value!;

        if (laterChange == LaterOptionChange.Retyped) {
            option.OptionName = "New choice";
            draft.EditContext.NotifyFieldChanged(new FieldIdentifier(option, nameof(option.OptionName)));
        } else if (laterChange == LaterOptionChange.Removed) {
            draft.Model.Options.Remove(option);
        }
        draft.Acknowledge(submission, accepted);

        Assert.Equal(accepted.Definition, draft.Model.ExpectedDefinition);
        Assert.DoesNotContain(accepted.Options, row => row.Id == originalId);
        if (laterChange == LaterOptionChange.Removed) {
            Assert.DoesNotContain(option, draft.Model.Options);
            Assert.Equal(originalId, option.Id);
            return;
        }
        Assert.Null(option.Id);
        Assert.Contains(option, draft.Model.Options);
        Assert.Equal(laterChange == LaterOptionChange.Retyped ? "New choice" : string.Empty, option.OptionName);
        if (laterChange == LaterOptionChange.Retyped) {
            var next = draft.Capture();
            var nextAccepted = (await store.SaveAsync(next.Model)).Value!;
            Assert.NotEqual(originalId, Assert.Single(nextAccepted.Options).Id);
        }
    }

    public enum LaterOptionChange { None, Retyped, Removed }
}
