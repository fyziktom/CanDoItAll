using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Home;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Tests.Components.Prompts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The production Home host composed with the real surface, the real route catalogue, the real secondary tabs and the
// real in-memory agent-context registry; only the application query is scripted.
public sealed class CrmHrHomePageTests
{
    private static readonly CrmHrHomeSnapshotModel Snapshot = new(
        PartyCount: 42,
        OrganizationCount: 7,
        OpportunityCount: 9,
        WorkforceProfileCount: 11,
        AgentProjectionCount: 2,
        SensitiveCount: 1,
        DirectoryPreview: [new CrmHrHomePartyPreviewModel(Guid.NewGuid(), "Aurora Logistics", PartyType.Organization, PartyLifecycleStatus.Active, false, "Regional account", DateTimeOffset.UnixEpoch)],
        SensitivePreview: [new CrmHrHomePartyPreviewModel(Guid.NewGuid(), "Dana Reyes", PartyType.Person, PartyLifecycleStatus.Candidate, true, "Hidden here", DateTimeOffset.UnixEpoch)],
        OpenPipelinePreview:
        [
            new OpportunitySummaryModel(
                Guid.Parse("71000000-0000-0000-0000-000000000002"),
                "Renewal expansion",
                OpportunityStage.Proposal,
                Guid.Parse("71000000-0000-0000-0000-000000000001"),
                Guid.NewGuid(),
                "Aurora Logistics",
                "Bram Vos",
                OpportunitySource.Renewal,
                45000m,
                65)
        ]);

    [Fact]
    public async Task Host_renders_the_shared_surface_reads_once_and_records_exactly_one_navigation_per_action()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var navigation = context.Services.GetRequiredService<BunitNavigationManager>();
        var authority = new Uri(navigation.BaseUri).GetLeftPart(UriPartial.Authority);

        var cut = context.Render<CrmHrHomePage>();

        var surface = cut.FindComponent<CrmHrHomeSurface>();
        Assert.Equal(CrmHrHomePhase.Loading, surface.Instance.Presentation.Phase);
        // The production secondary tabs are composed into the surface's host-owned slot.
        var tabs = cut.FindComponent<CanDoItAll.Modules.CrmHr.Components.CrmHrSecondaryTabs>();
        Assert.Equal(CrmHrWorkspaceArea.Home, tabs.Instance.SelectedArea);
        Assert.NotNull(cut.Find("[data-testid='crmhr-home'] [aria-label='Home tab information']"));
        // The host flips the surface to interactive after its first interactive render.
        Assert.Equal("true", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-interactive"));

        await cut.InvokeAsync(() => query.Complete(Snapshot));
        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));
        Assert.Contains("42", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Renewal expansion", cut.Markup, StringComparison.Ordinal);

        // A same-instance rerender issues no new request.
        cut.Render();
        Assert.Single(query.Calls);
        Assert.Empty(navigation.History);

        // Every action records exactly one navigation, to the catalogue route of its destination.
        var expectedNavigations = 0;
        foreach (var (testId, area) in new[]
                 {
                     ("crmhr-home-open-directory", CrmHrWorkspaceArea.Directory),
                     ("crmhr-home-route-crm", CrmHrWorkspaceArea.Crm),
                     ("crmhr-home-route-workforce", CrmHrWorkspaceArea.Workforce),
                     ("crmhr-home-route-recruiting", CrmHrWorkspaceArea.Recruiting),
                     ("crmhr-home-route-agents", CrmHrWorkspaceArea.Agents),
                     ("crmhr-home-route-assignments", CrmHrWorkspaceArea.Assignments),
                     ("crmhr-home-sensitive-open-directory", CrmHrWorkspaceArea.Directory),
                     ("crmhr-home-open-crm", CrmHrWorkspaceArea.Crm)
                 })
        {
            cut.Find($"[data-testid='{testId}']").Click();
            expectedNavigations++;
            Assert.Equal(expectedNavigations, navigation.History.Count);
            Assert.Equal(authority + CrmHrRouteCatalog.Get(area).Route, navigation.Uri);
        }

        cut.Find("[data-testid='crmhr-home-opportunity-open']").Click();
        expectedNavigations++;
        Assert.Equal(expectedNavigations, navigation.History.Count);
        Assert.EndsWith(
            "/crm-hr/crm?accountId=71000000-0000-0000-0000-000000000001&opportunityId=71000000-0000-0000-0000-000000000002",
            navigation.Uri,
            StringComparison.Ordinal);
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task Host_publishes_the_static_home_agent_context_keeps_it_on_query_failure_and_releases_it_when_its_owner_removes_home()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var expected = CrmHrAgentChatSurfaceBuilder.BuildHomeSurface();

        var owner = RenderHomeInsideOwner(context);
        var cut = owner.FindComponent<CrmHrHomePage>();

        var snapshot = registry.Capture();
        Assert.NotNull(snapshot);
        Assert.Equal(expected.Source, snapshot!.Scope.Source);
        Assert.Equal("CRM / HR", snapshot.Scope.DisplayName);
        Assert.Equal("/crm-hr", snapshot.Scope.SurfacePosition?.Route);
        Assert.Empty(snapshot.Fragments);

        await cut.InvokeAsync(() => query.Fail(new InvalidOperationException("relation does not exist")));
        cut.WaitForAssertion(() => Assert.Equal("failed", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));

        // A failed read never touches the route's static context and never leaks preview or error data into it.
        var afterFailure = registry.Capture();
        Assert.NotNull(afterFailure);
        Assert.Equal(expected.Source, afterFailure!.Scope.Source);
        Assert.Equal(snapshot.Scope.Id, afterFailure.Scope.Id);
        Assert.Empty(afterFailure.Fragments);
        Assert.DoesNotContain("relation does not exist", cut.Markup, StringComparison.Ordinal);

        // The real owner stops rendering Home; its scope registration goes with it and nothing else remains.
        await owner.InvokeAsync(owner.Instance.Hide);

        owner.WaitForAssertion(() => Assert.Empty(owner.FindComponents<CrmHrHomePage>()));
        Assert.Null(registry.Capture());
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task Removing_home_leaves_an_unrelated_registry_scope_valid_and_a_recreated_home_starts_a_fresh_read()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();

        var owner = RenderHomeInsideOwner(context);
        var first = owner.FindComponent<CrmHrHomePage>();
        var firstInstance = first.Instance;
        await first.InvokeAsync(() => query.Fail(new IOException("offline")));
        first.WaitForAssertion(() => Assert.Equal("failed", first.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));

        // A distinct scope takes over the same real registry after Home, as another surface would.
        var directorySurface = CrmHrAgentChatSurfaceBuilder.BuildDirectorySurface();
        using var unrelated = registry.ActivateScope(directorySurface.ToScope(AgentChatContextScopeId.Create()));
        Assert.True(unrelated.IsActive);
        Assert.Equal(unrelated.ScopeId, registry.Capture()?.Scope.Id);

        await owner.InvokeAsync(owner.Instance.Hide);
        owner.WaitForAssertion(() => Assert.Empty(owner.FindComponents<CrmHrHomePage>()));

        // Home released only its own registration: the unrelated scope is still the valid active scope.
        Assert.True(unrelated.IsActive);
        var afterRemoval = registry.Capture();
        Assert.NotNull(afterRemoval);
        Assert.Equal(unrelated.ScopeId, afterRemoval!.Scope.Id);
        Assert.Single(query.Calls);

        // A recreated Home is a fresh instance with its own read; it inherits neither the failure nor the old session.
        var second = context.Render<CrmHrHomePage>();
        Assert.Equal("loading", second.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        Assert.Equal(2, query.Calls.Count);
        Assert.NotSame(firstInstance, second.Instance);

        await second.InvokeAsync(() => query.Complete(Snapshot));

        second.WaitForAssertion(() => Assert.Equal("ready", second.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));
        Assert.Equal(CrmHrHomePhase.Failed, firstInstance.Presentation.Phase);
        Assert.Equal(CrmHrAgentChatSurfaceBuilder.BuildHomeSurface().Source, registry.Capture()?.Scope.Source);
        Assert.False(unrelated.IsActive);
    }

    [Fact]
    public async Task Failed_read_offers_retry_and_a_retry_reads_again_through_the_same_instance()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);

        var cut = context.Render<CrmHrHomePage>();
        await cut.InvokeAsync(() => query.Fail(new IOException("offline")));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-home-retry']")));
        Assert.Contains(CrmHrHomeText.UnavailableValue, cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-home-retry']").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='crmhr-home-retry']").HasAttribute("disabled")));
        Assert.Equal(2, query.Calls.Count);

        await cut.InvokeAsync(() => query.Complete(Snapshot));

        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));
        Assert.Contains("42", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, query.Calls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_disposed_host_ignores_the_late_outcome_of_a_query_that_ignores_cancellation(bool fail)
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var cut = context.Render<CrmHrHomePage>();
        var page = cut.Instance;
        var read = page.ReadCompletion;
        Assert.Single(query.Calls);
        Assert.False(read.IsCompleted);

        await context.DisposeRenderedComponentsAsync();
        Assert.True(query.Calls[0].IsCancellationRequested);

        if (fail)
        {
            query.Fail(new IOException("late failure"));
        }
        else
        {
            query.Complete(Snapshot);
        }

        // The read completes after its late outcome was fenced; only then is the accepted presentation inspected.
        await read.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CrmHrHomePhase.Loading, page.Presentation.Phase);
        Assert.Null(page.Presentation.Overview);
        Assert.Null(page.Presentation.FailureMessage);
    }

    [Fact]
    public async Task Two_live_instances_keep_separate_state_and_the_second_does_not_inherit_the_first_failure()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var first = context.Render<CrmHrHomePage>();
        await first.InvokeAsync(() => query.Fail(new IOException("offline")));
        first.WaitForAssertion(() => Assert.Equal("failed", first.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));

        var second = context.Render<CrmHrHomePage>();
        Assert.Equal("loading", second.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        await second.InvokeAsync(() => query.Complete(Snapshot));

        second.WaitForAssertion(() => Assert.Equal("ready", second.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));
        Assert.Equal("failed", first.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        Assert.Equal(2, query.Calls.Count);
    }

    // Home rendered by a real conditional owner, so a test can remove exactly Home while the renderer stays alive.
    private static IRenderedComponent<ConditionalRenderHost> RenderHomeInsideOwner(BunitContext context)
        => context.Render<ConditionalRenderHost>(parameters => parameters
            .AddChildContent<CrmHrHomePage>());

    private static BunitContext CreateContext(ScriptedHomeQuery query)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ICrmHrHomeQueryService>(query);
        context.Services.AddSingleton<IAgentChatContextRegistry>(new AgentChatContextRegistry(TimeProvider.System));
        return context;
    }

    // Completes only when the test says so and never observes cancellation.
    private sealed class ScriptedHomeQuery : ICrmHrHomeQueryService
    {
        private readonly Queue<TaskCompletionSource<CrmHrHomeSnapshotModel>> pending = new();

        public List<CancellationToken> Calls { get; } = [];

        public Task<CrmHrHomeSnapshotModel> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add(cancellationToken);
            var source = new TaskCompletionSource<CrmHrHomeSnapshotModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue(source);
            return source.Task;
        }

        public void Complete(CrmHrHomeSnapshotModel snapshot) => pending.Dequeue().SetResult(snapshot);

        public void Fail(Exception exception) => pending.Dequeue().SetException(exception);
    }
}
