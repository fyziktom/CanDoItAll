using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.TestLab.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.TestLab;

public sealed class TestLabNotificationTests : BunitContext {
    [Theory]
    [InlineData(TestLabWriteOutcome.Unknown, TestLabSaveState.Unknown, "Test plan save outcome unknown", NotificationSeverity.Error)]
    [InlineData(TestLabWriteOutcome.Refused, TestLabSaveState.Refused, "Test plan save failed", NotificationSeverity.Error)]
    [InlineData(TestLabWriteOutcome.Committed, TestLabSaveState.Saved, "Test plan saved", NotificationSeverity.Success)]
    [InlineData(TestLabWriteOutcome.CommittedWithWarning, TestLabSaveState.SavedWithWarning, "Test plan saved; refresh incomplete", NotificationSeverity.Warning)]
    public async Task Real_page_notifies_the_owner_outcome_without_replaying_on_refresh_or_project_change(
        TestLabWriteOutcome outcome, TestLabSaveState state, string title, NotificationSeverity severity) {
        var owner = new Owner(outcome);
        Services.AddLogging();
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<ITestLabWorkspaceOwner>(owner);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<TestLabPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("form")));
        var view = cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "Submitted" }));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        var notification = Assert.Single(Services.GetRequiredService<NotificationService>().Messages);
        Assert.Equal(title, notification.Summary);
        Assert.Equal(severity, notification.Severity);
        Assert.Equal(state, draft.SaveState);
        if (outcome == TestLabWriteOutcome.Unknown) {
            Assert.Equal(Owner.UncertainMessage, notification.Detail);
            Assert.NotNull(draft.UncertainSubmission);
            Assert.False(draft.CanSave);
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-retry]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-project-select]").ChangeAsync(new() { Value = owner.Project.Id.ToString() }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-project-select]").ChangeAsync(new() { Value = string.Empty }));
        if (outcome == TestLabWriteOutcome.Unknown) {
            await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
            Assert.False(draft.CanSave);
            Assert.Equal(TestLabSaveState.Unknown, draft.SaveState);
            Assert.NotNull(draft.UncertainSubmission);
            Assert.True(cut.Find("[data-testid=testlab-save-button]").HasAttribute("disabled"));
        }
        Assert.Same(draft, view.State.Draft);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal(1, owner.Writes);
        Assert.Single(Services.GetRequiredService<NotificationService>().Messages);
    }

    private sealed class Owner(TestLabWriteOutcome outcome) : ITestLabWorkspaceOwner {
        public const string UncertainMessage = "Save outcome is unknown. Review stored plans; refresh does not retry this write.";
        private TestPlanEditorModel? stored;
        public int Writes { get; private set; }
        public TestLabProjectOption Project { get; } = CreateProject();
        public Task<IReadOnlyList<TestLabProjectOption>> ProjectsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestLabProjectOption>>([Project]);
        public Task<IReadOnlyList<TestLabPartyOption>> PartiesAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestLabPartyOption>>([]);
        public Task<TestLabPartyOption?> PartyAsync(Guid partyId, CancellationToken cancellationToken) => Task.FromResult<TestLabPartyOption?>(null);
        public Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestPlanSummary>>([]);
        public Task<TestPlanEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(stored is null ? null : TestLabSubmission.Clone(stored));
        public Task<TestLabWriteResult> SaveAsync(TestPlanEditorModel submission) {
            Writes++;
            if (outcome is TestLabWriteOutcome.Committed or TestLabWriteOutcome.CommittedWithWarning) {
                submission.Id ??= Guid.NewGuid();
                stored = TestLabSubmission.Clone(submission);
            }
            return Task.FromResult(new TestLabWriteResult(outcome, stored?.Id,
                outcome == TestLabWriteOutcome.Unknown ? UncertainMessage : "Controlled owner outcome."));
        }
        private static TestLabProjectOption CreateProject() {
            var id = Guid.NewGuid();
            return new(id, "Current project", new(Guid.NewGuid(), id, Guid.NewGuid()));
        }
    }
}
