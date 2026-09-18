using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Controlled rendering of the Home surface: no query service, EF, module runtime or host registration is involved.
public sealed class CrmHrHomeSurfaceTests
{
    private static readonly Guid AccountId = Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid OpportunityId = Guid.Parse("61000000-0000-0000-0000-000000000002");

    [Fact]
    public void Ready_presentation_renders_totals_previews_and_optional_values()
    {
        using var context = CreateContext();
        var overview = new CrmHrHomeOverview(
            new CrmHrHomeTotals(Parties: 1284, Organizations: 311, Opportunities: 97, WorkforceProfiles: 542, AgentProjections: 18, SensitiveRecords: 41),
            [
                new CrmHrHomeDirectoryEntry(Guid.NewGuid(), "Aurora Logistics", "Organization", "Active", "Regional logistics account.", IsSensitive: false),
                new CrmHrHomeDirectoryEntry(Guid.NewGuid(), "Dana Reyes", "Person", "Candidate", null, IsSensitive: true)
            ],
            [new CrmHrHomeSensitiveEntry(Guid.NewGuid(), "Dana Reyes", "Person", "Candidate")],
            [
                new CrmHrHomeOpportunityEntry(OpportunityId, AccountId, "Renewal expansion", "Aurora Logistics", "Bram Vos", "Proposal", "Renewal", 45000m, 65),
                new CrmHrHomeOpportunityEntry(Guid.NewGuid(), AccountId, "Discovery call", "Unknown account", "Unknown owner", "Identified", "Direct", null, 0)
            ]);

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateReady(1, overview)));

        Assert.Equal("ready", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        Assert.Contains("1284", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.Contains("311", cut.Find("[data-testid='crmhr-home-stat-organizations']").TextContent, StringComparison.Ordinal);
        Assert.Contains("97", cut.Find("[data-testid='crmhr-home-stat-pipeline']").TextContent, StringComparison.Ordinal);
        Assert.Contains("542", cut.Find("[data-testid='crmhr-home-stat-workforce']").TextContent, StringComparison.Ordinal);
        Assert.Contains("18", cut.Find("[data-testid='crmhr-home-stat-agents']").TextContent, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("[data-testid='crmhr-home-directory-item']").Count);
        Assert.Contains("41 sensitive record(s)", cut.Find("[data-testid='crmhr-home-sensitive-card']").TextContent, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("[data-testid='crmhr-home-sensitive-item']"));
        Assert.Contains("Regional logistics account.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Opportunities: 97. Workforce profiles: 542. Agent projections: 18.", cut.Find("[data-testid='crmhr-home-signal']").TextContent, StringComparison.Ordinal);

        var opportunities = cut.FindAll("[data-testid='crmhr-home-opportunity-item']");
        Assert.Equal(2, opportunities.Count);
        Assert.Contains("Aurora Logistics / Proposal / Renewal", opportunities[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Owner: Bram Vos", opportunities[0].TextContent, StringComparison.Ordinal);
        Assert.Contains($"{45000m:0.##} / 65%", opportunities[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Unknown account / Identified / Direct", opportunities[1].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("%", opportunities[1].TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-directory-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-sensitive-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-pipeline-empty']"));
    }

    [Fact]
    public void Loading_renders_placeholders_and_no_empty_copy_while_navigation_stays_usable()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrHomeIntent>();

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateLoading(3))
            .Add(component => component.Intent, EventCallback.Factory.Create<CrmHrHomeIntent>(this, intents.Add)));

        Assert.Equal("loading", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        Assert.Contains(CrmHrHomeText.LoadingValue, cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(">0<", cut.Find("[data-testid='crmhr-home-stat-parties']").OuterHtml, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='crmhr-home-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-sensitive-card']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-directory-empty']"));
        Assert.DoesNotContain("No open opportunities", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("No sensitive parties", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Counts are loading.", cut.Find("[data-testid='crmhr-home-signal']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-home-route-workforce']").Click();
        cut.Find("[data-testid='crmhr-home-open-directory']").Click();

        Assert.Equal(
            [CrmHrHomeDestination.Workforce, CrmHrHomeDestination.Directory],
            intents.Cast<CrmHrHomeIntent.Navigate>().Select(intent => intent.Destination));
    }

    [Fact]
    public void Failed_renders_safe_copy_and_retry_and_never_zero_values()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrHomeIntent>();

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateFailed(5, "The CRM / HR overview could not be loaded."))
            .Add(component => component.Intent, EventCallback.Factory.Create<CrmHrHomeIntent>(this, intents.Add)));

        Assert.Equal("failed", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-phase"));
        Assert.Contains(CrmHrHomeText.UnavailableValue, cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.Contains("The CRM / HR overview could not be loaded.", cut.Find("[data-testid='crmhr-home-failed']").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-sensitive-card']"));
        Assert.Contains("Counts are unavailable", cut.Find("[data-testid='crmhr-home-signal']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-home-retry']").Click();
        cut.Find("[data-testid='crmhr-home-failed-open-directory']").Click();

        Assert.Equal(5, Assert.IsType<CrmHrHomeIntent.Retry>(intents[0]).Generation);
        Assert.Equal(CrmHrHomeDestination.Directory, Assert.IsType<CrmHrHomeIntent.Navigate>(intents[1]).Destination);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateFailed(6, "The CRM / HR overview could not be loaded.", isRetrying: true)));
        Assert.True(cut.Find("[data-testid='crmhr-home-retry']").HasAttribute("disabled"));
    }

    [Fact]
    public void Empty_ready_overview_shows_the_section_empty_copy_and_the_create_party_action()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrHomeIntent>();

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateReady(2, CrmHrHomeOverview.Empty))
            .Add(component => component.Intent, EventCallback.Factory.Create<CrmHrHomeIntent>(this, intents.Add)));

        Assert.Contains("0", cut.Find("[data-testid='crmhr-home-stat-parties']").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-testid='crmhr-home-directory-empty']"));
        Assert.Contains("No sensitive parties are currently flagged", cut.Find("[data-testid='crmhr-home-sensitive-empty']").TextContent, StringComparison.Ordinal);
        Assert.Contains("No open opportunities are currently tracked.", cut.Find("[data-testid='crmhr-home-pipeline-empty']").TextContent, StringComparison.Ordinal);
        Assert.Contains("0 sensitive record(s)", cut.Find("[data-testid='crmhr-home-sensitive-card']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-home-create-party']").Click();

        Assert.Equal(CrmHrHomeDestination.Directory, Assert.IsType<CrmHrHomeIntent.Navigate>(Assert.Single(intents)).Destination);
    }

    [Fact]
    public void Every_navigation_action_emits_its_destination_and_the_opportunity_action_carries_both_identifiers()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrHomeIntent>();
        var overview = new CrmHrHomeOverview(
            new CrmHrHomeTotals(1, 0, 1, 0, 0, 0),
            [],
            [],
            [new CrmHrHomeOpportunityEntry(OpportunityId, AccountId, "Renewal", "Account", "Owner", "Proposal", "Direct", 10m, 50)]);

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateReady(1, overview))
            .Add(component => component.Intent, EventCallback.Factory.Create<CrmHrHomeIntent>(this, intents.Add)));

        foreach (var testId in new[]
                 {
                     "crmhr-home-open-directory",
                     "crmhr-home-route-directory",
                     "crmhr-home-route-crm",
                     "crmhr-home-route-workforce",
                     "crmhr-home-route-recruiting",
                     "crmhr-home-route-agents",
                     "crmhr-home-route-assignments",
                     "crmhr-home-sensitive-open-directory",
                     "crmhr-home-open-crm"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Click();
        }

        cut.Find("[data-testid='crmhr-home-opportunity-open']").Click();

        Assert.Equal(
            [
                CrmHrHomeDestination.Directory,
                CrmHrHomeDestination.Directory,
                CrmHrHomeDestination.Crm,
                CrmHrHomeDestination.Workforce,
                CrmHrHomeDestination.Recruiting,
                CrmHrHomeDestination.Agents,
                CrmHrHomeDestination.Assignments,
                CrmHrHomeDestination.Directory,
                CrmHrHomeDestination.Crm
            ],
            intents.Take(9).Cast<CrmHrHomeIntent.Navigate>().Select(intent => intent.Destination));
        var open = Assert.IsType<CrmHrHomeIntent.OpenOpportunity>(intents[9]);
        Assert.Equal(AccountId, open.AccountPartyId);
        Assert.Equal(OpportunityId, open.OpportunityId);
        Assert.Equal(10, intents.Count);
    }

    [Fact]
    public void Untrusted_text_is_rendered_as_text_and_the_sensitive_card_carries_no_summary()
    {
        using var context = CreateContext();
        const string markupName = "<script>alert('directory')</script> Untrusted & Co";
        const string summary = "<b>Operational summary</b> only for the directory row";
        var partyId = Guid.NewGuid();
        var overview = new CrmHrHomeOverview(
            new CrmHrHomeTotals(2, 0, 1, 0, 0, 1),
            [new CrmHrHomeDirectoryEntry(partyId, markupName, "Person", "Active", summary, IsSensitive: true)],
            [new CrmHrHomeSensitiveEntry(partyId, markupName, "Person", "Active")],
            [new CrmHrHomeOpportunityEntry(OpportunityId, AccountId, "<img src=x onerror=alert(1)>", "<i>Account</i>", "Owner", "Proposal", "Direct", null, 0)]);

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateReady(1, overview)));

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-home-directory-item'] b"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Untrusted &amp; Co", cut.Markup, StringComparison.Ordinal);
        var sensitiveCard = cut.Find("[data-testid='crmhr-home-sensitive-card']");
        Assert.Contains(markupName, sensitiveCard.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Operational summary", sensitiveCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("Operational summary", cut.Find("[data-testid='crmhr-home-directory-item']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Secondary_navigation_slot_renders_host_content_between_the_header_and_the_cards()
    {
        using var context = CreateContext();

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateLoading(1))
            .Add(component => component.SecondaryNavigation, builder => builder.AddMarkupContent(0, "<nav data-testid='host-tabs'>tabs</nav>")));

        var slot = cut.Find("[data-testid='crmhr-home'] > [data-testid='host-tabs']");
        Assert.Equal("tabs", slot.TextContent);
    }

    [Fact]
    public void A_non_interactive_host_reports_the_signal_and_keeps_every_action_disabled_until_it_flips()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrHomeIntent>();
        var overview = new CrmHrHomeOverview(
            new CrmHrHomeTotals(1, 1, 1, 0, 0, 1),
            [new CrmHrHomeDirectoryEntry(Guid.NewGuid(), "Aurora Logistics", "Organization", "Active", null, IsSensitive: false)],
            [new CrmHrHomeSensitiveEntry(Guid.NewGuid(), "Dana Reyes", "Person", "Candidate")],
            [new CrmHrHomeOpportunityEntry(OpportunityId, AccountId, "Renewal expansion", "Aurora Logistics", "Bram Vos", "Proposal", "Renewal", 45000m, 65)]);

        var cut = context.Render<CrmHrHomeSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrHomePresentation.CreateReady(1, overview))
            .Add(component => component.IsInteractive, false)
            .Add(component => component.Intent, intent => intents.Add(intent)));

        Assert.Equal("false", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-interactive"));
        var actions = cut.FindAll("[data-testid='crmhr-home'] button, [data-testid='crmhr-home-open-directory']");
        Assert.NotEmpty(actions);
        Assert.All(actions, action => Assert.True(action.HasAttribute("disabled"), $"{action.GetAttribute("data-testid")} is enabled"));

        cut.Render(parameters => parameters.Add(component => component.IsInteractive, true));

        Assert.Equal("true", cut.Find("[data-testid='crmhr-home']").GetAttribute("data-interactive"));
        Assert.All(cut.FindAll("[data-testid='crmhr-home'] button"), action => Assert.False(action.HasAttribute("disabled")));
        cut.Find("[data-testid='crmhr-home-route-crm']").Click();
        Assert.Equal(CrmHrHomeDestination.Crm, Assert.IsType<CrmHrHomeIntent.Navigate>(Assert.Single(intents)).Destination);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
