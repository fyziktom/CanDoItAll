using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Home;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
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
    public async Task Host_renders_the_shared_surface_reads_once_and_maps_every_navigation_to_the_catalogue_exactly_once()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var navigation = context.Services.GetRequiredService<NavigationManager>();

        var cut = context.Render<CrmHrHomePage>();

        var surface = cut.FindComponent<CrmHrHomeSurface>();
        Assert.Equal(CrmHrHomePhase.Loading, surface.Instance.Presentation.Phase);
        // The production secondary tabs are composed into the surface's host-owned slot.
        var tabs = cut.FindComponent<CanDoItAll.Modules.CrmHr.Components.CrmHrSecondaryTabs>();
        Assert.Equal(CrmHrWorkspaceArea.Home, tabs.Instance.SelectedArea);
        Assert.NotNull(cut.Find("[data-testid='crmhr-home'] [aria-label='Home tab information']"));

        await cut.InvokeAsync(() => query.Complete(Snapshot));
        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase")));
        Assert.Contains("42", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Renewal expansion", cut.Markup, StringComparison.Ordinal);

        // A same-instance rerender issues no new request.
        cut.Render();
        Assert.Single(query.Calls);

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
            Assert.Equal(new Uri(navigation.BaseUri).GetLeftPart(UriPartial.Authority) + CrmHrRouteCatalog.Get(area).Route, navigation.Uri);
        }

        cut.Find("[data-testid='crmhr-home-opportunity-open']").Click();
        Assert.EndsWith(
            "/crm-hr/crm?accountId=71000000-0000-0000-0000-000000000001&opportunityId=71000000-0000-0000-0000-000000000002",
            navigation.Uri,
            StringComparison.Ordinal);
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task Host_publishes_the_static_home_agent_context_keeps_it_on_query_failure_and_releases_only_its_own_scope_on_disposal()
    {
        var query = new ScriptedHomeQuery();
        using var context = CreateContext(query);
        var registry = context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var expected = CrmHrAgentChatSurfaceBuilder.BuildHomeSurface();

        var cut = context.Render<CrmHrHomePage>();

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
        Assert.Empty(afterFailure.Fragments);
        Assert.DoesNotContain("relation does not exist", cut.Markup, StringComparison.Ordinal);

        await context.DisposeComponentsAsync();

        Assert.Null(registry.Capture());
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
        Assert.Single(query.Calls);

        await context.DisposeComponentsAsync();
        Assert.True(query.Calls[0].IsCancellationRequested);

        if (fail)
        {
            query.Fail(new IOException("late failure"));
        }
        else
        {
            query.Complete(Snapshot);
        }

        await Task.Yield();

        Assert.Equal(CrmHrHomePhase.Loading, page.Presentation.Phase);
        Assert.Null(page.Presentation.Overview);
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
            var source = new TaskCompletionSource<CrmHrHomeSnapshotModel>();
            pending.Enqueue(source);
            return source.Task;
        }

        public void Complete(CrmHrHomeSnapshotModel snapshot) => pending.Dequeue().SetResult(snapshot);

        public void Fail(Exception exception) => pending.Dequeue().SetException(exception);
    }
}
