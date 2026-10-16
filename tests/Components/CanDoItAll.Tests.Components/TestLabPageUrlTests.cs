using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.TestLab.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.TestLab;

public sealed class TestLabPageUrlTests : BunitContext {
    [Fact]
    public async Task Page_applies_a_deep_link_and_records_section_filter_selection_and_new_plan_with_their_history_rules() {
        var owner = new Owner();
        Services.AddLogging();
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<ITestLabWorkspaceOwner>(owner);
        JSInterop.Mode = JSRuntimeMode.Loose;
        // The page reads RendererInfo; setting it builds the renderer, so it follows every registration.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/test-lab?planId={owner.First:D}&section=Cases&q=%20Alpha%20&utm=mail");

        var cut = Render<TestLabPage>();
        var view = cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;

        cut.WaitForAssertion(() => {
            Assert.Equal(owner.First, view.State.Draft?.Model.Id);
            Assert.Equal(TestLabSection.Cases, view.State.Section);
            Assert.Equal("Alpha", view.State.Filter.Search);
        });
        Assert.Equal($"/test-lab?planId={owner.First:D}&section=cases&q=Alpha&utm=mail", Location(navigation));
        Assert.Single(navigation.History);

        await TabAsync(cut, "Evidence");
        Assert.Equal($"/test-lab?planId={owner.First:D}&section=evidence&q=Alpha&utm=mail", Location(navigation));
        Assert.Equal(2, navigation.History.Count);

        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-search]").InputAsync(new() { Value = "plan" }));
        Assert.Equal($"/test-lab?planId={owner.First:D}&section=evidence&q=plan&utm=mail", Location(navigation));
        Assert.Equal(2, navigation.History.Count);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);

        await cut.InvokeAsync(() => view.SelectAsync(owner.Second));
        cut.WaitForAssertion(() => Assert.Equal(owner.Second, view.State.Draft?.Model.Id));
        Assert.Equal($"/test-lab?planId={owner.Second:D}&q=plan&utm=mail", Location(navigation));
        Assert.Equal(TestLabSection.Overview, view.State.Section);
        Assert.Equal(3, navigation.History.Count);

        // A location of the first plan (Back) selects it again with its section.
        navigation.NavigateTo($"/test-lab?planId={owner.First:D}&section=evidence&q=plan&utm=mail");
        cut.WaitForAssertion(() => {
            Assert.Equal(owner.First, view.State.Draft?.Model.Id);
            Assert.Equal(TestLabSection.Evidence, view.State.Section);
        });

        await cut.InvokeAsync(view.NewAsync);
        Assert.Equal("/test-lab?q=plan&utm=mail", Location(navigation));
        Assert.Null(view.State.Draft?.Model.Id);
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").ChangeAsync(new() { Value = "Gamma plan" }));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        var created = Assert.IsType<Guid>(view.State.Draft?.Model.Id);
        Assert.Equal($"/test-lab?planId={created:D}&q=plan&utm=mail", Location(navigation));
        Assert.Equal(entries, navigation.History.Count);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public void Page_reports_an_unknown_plan_without_replacing_it() {
        var owner = new Owner();
        var missing = Guid.NewGuid();
        Services.AddLogging();
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<ITestLabWorkspaceOwner>(owner);
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/test-lab?planId={missing:D}");

        var cut = Render<TestLabPage>();

        cut.WaitForAssertion(() => Assert.Contains("Requested plan was not found", cut.Find("[data-testid=testlab-editor-state]").TextContent));
        Assert.Empty(cut.FindAll("form"));
        Assert.Equal($"/test-lab?planId={missing:D}", Location(navigation));
    }

    private static string Location(NavigationManager navigation) => "/" + navigation.ToBaseRelativePath(navigation.Uri);

    private static Task TabAsync(IRenderedComponent<TestLabPage> cut, string label) =>
        cut.InvokeAsync(() => cut.FindComponent<SecondaryTabs>().FindAll("button")
            .Single(button => button.TextContent.Trim().StartsWith(label, StringComparison.Ordinal)).ClickAsync(new()));

    private sealed class Owner : ITestLabWorkspaceOwner {
        private readonly Dictionary<Guid, TestPlanEditorModel> stored = [];

        public Owner() {
            stored[First] = new() { Id = First, Title = "Alpha plan", Phase = "Browser phase" };
            stored[Second] = new() { Id = Second, Title = "Beta plan", Phase = "Browser phase" };
        }

        public Guid First { get; } = Guid.NewGuid();
        public Guid Second { get; } = Guid.NewGuid();
        public int Writes { get; private set; }

        public Task<IReadOnlyList<TestLabProjectOption>> ProjectsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestLabProjectOption>>([]);
        public Task<IReadOnlyList<TestLabPartyOption>> PartiesAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestLabPartyOption>>([]);
        public Task<TestLabPartyOption?> PartyAsync(Guid partyId, CancellationToken cancellationToken) => Task.FromResult<TestLabPartyOption?>(null);
        public Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TestPlanSummary>>(stored.Values.Select(plan => new TestPlanSummary(plan.Id!.Value, plan.ProjectId, plan.Title,
                plan.Phase, plan.Cases.Count, plan.Evidence.Count, null, DateTimeOffset.UtcNow)).ToArray());
        public Task<TestPlanEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(stored.TryGetValue(id, out var plan) ? TestLabSubmission.Clone(plan) : null);
        public Task<TestLabWriteResult> SaveAsync(TestPlanEditorModel submission) {
            Writes++;
            submission.Id ??= Guid.NewGuid();
            stored[submission.Id.Value] = TestLabSubmission.Clone(submission);
            return Task.FromResult(new TestLabWriteResult(TestLabWriteOutcome.Committed, submission.Id, "Controlled owner outcome."));
        }
    }
}
