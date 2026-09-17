using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Controlled rendering of the account summary surface: no service, module runtime or workspace model is involved.
public sealed class CrmHrAccountSummarySurfaceTests
{
    private static readonly Guid AccountId = Guid.Parse("82000000-0000-0000-0000-000000000001");

    [Fact]
    public void No_account_renders_the_empty_state_and_its_directory_intent_names_no_account()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrAccountSummaryIntent>();

        var cut = context.Render<CrmHrAccountSummarySurface>(parameters => parameters
            .Add(component => component.Account, null)
            .Add(component => component.Intent, intent => intents.Add(intent)));

        Assert.NotNull(cut.Find("[data-testid='crmhr-account-summary-empty']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-summary']"));
        Assert.Contains("Choose an account from the list", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-account-summary-open-directory']").Click();

        var open = Assert.IsType<CrmHrAccountSummaryIntent.OpenDirectory>(Assert.Single(intents));
        Assert.Null(open.AccountPartyId);
    }

    [Fact]
    public void Account_renders_labels_tones_contacts_roles_counts_and_the_conversion_offer()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrAccountSummaryIntent>();
        var account = Summary() with { PrimaryPhone = null };

        var cut = context.Render<CrmHrAccountSummarySurface>(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Intent, intent => intents.Add(intent)));

        var root = cut.Find("[data-testid='crmhr-account-summary']");
        Assert.Equal(AccountId.ToString("D"), root.GetAttribute("data-account-id"));
        Assert.Equal("Aurora Logistics", cut.Find("[data-testid='crmhr-account-summary-name']").TextContent);
        Assert.Equal("Regional logistics account.", cut.Find("[data-testid='crmhr-account-summary-text']").TextContent);
        Assert.Equal("Prospect", cut.Find("[data-testid='crmhr-account-summary-stage']").TextContent.Trim());
        Assert.Equal("Active", cut.Find("[data-testid='crmhr-account-summary-lifecycle']").TextContent.Trim());
        var badges = cut.FindComponents<StatusBadge>();
        Assert.Equal("info", Assert.Single(badges, badge => badge.Instance.Text == "Prospect").Instance.Tone);
        Assert.Equal("success", Assert.Single(badges, badge => badge.Instance.Text == "Active").Instance.Tone);
        var stats = cut.FindComponents<CompactStat>().Select(stat => (stat.Instance.Label, stat.Instance.Value)).ToArray();
        Assert.Equal(
            new[]
            {
                ("email", "accounts@aurora.example"),
                ("phone", CrmHrAccountText.NotSetValue),
                ("roles", "Customer, Partner"),
                ("connections", "3"),
                ("opportunities", "2"),
                ("tags", "4")
            },
            stats);

        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();
        cut.Find("[data-testid='crmhr-account-summary-open-directory']").Click();

        Assert.Equal(2, intents.Count);
        Assert.Equal(AccountId, Assert.IsType<CrmHrAccountSummaryIntent.ConvertToActiveCustomer>(intents[0]).AccountPartyId);
        Assert.Equal(AccountId, Assert.IsType<CrmHrAccountSummaryIntent.OpenDirectory>(intents[1]).AccountPartyId);
    }

    [Fact]
    public void An_active_customer_is_not_offered_the_conversion_and_missing_values_show_their_placeholders()
    {
        using var context = CreateContext();
        var account = Summary() with
        {
            Summary = null,
            RelationshipStageLabel = "ActiveCustomer",
            RelationshipStageTone = CrmHrAccountTone.Success,
            PrimaryEmail = null,
            PrimaryPhone = "   ",
            Roles = [],
            CanConvertToActiveCustomer = false
        };

        var cut = context.Render<CrmHrAccountSummarySurface>(parameters => parameters
            .Add(component => component.Account, account));

        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-convert-active-button']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-summary-text']"));
        var stats = cut.FindComponents<CompactStat>().Select(stat => stat.Instance.Value).ToArray();
        Assert.Equal(new[] { CrmHrAccountText.NotSetValue, CrmHrAccountText.NotSetValue, CrmHrAccountText.NoRolesValue, "3", "2", "4" }, stats);
    }

    [Fact]
    public void Untrusted_text_is_rendered_as_text()
    {
        using var context = CreateContext();
        var account = Summary() with
        {
            DisplayName = "<script>alert('account')</script> Untrusted & Co",
            Summary = "<b>Bold</b> summary that must stay text"
        };

        var cut = context.Render<CrmHrAccountSummarySurface>(parameters => parameters
            .Add(component => component.Account, account));

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-summary-text'] b"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Untrusted &amp; Co", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_non_interactive_host_reports_the_signal_and_keeps_the_actions_disabled_until_it_flips()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrAccountSummaryIntent>();

        var cut = context.Render<CrmHrAccountSummarySurface>(parameters => parameters
            .Add(component => component.Account, Summary())
            .Add(component => component.IsInteractive, false)
            .Add(component => component.Intent, intent => intents.Add(intent)));

        Assert.Equal("false", cut.Find("[data-testid='crmhr-account-summary']").GetAttribute("data-interactive"));
        var actions = cut.FindAll("[data-testid='crmhr-account-summary'] button");
        Assert.Equal(2, actions.Count);
        Assert.All(actions, action => Assert.True(action.HasAttribute("disabled")));

        cut.Render(parameters => parameters.Add(component => component.IsInteractive, true));

        Assert.Equal("true", cut.Find("[data-testid='crmhr-account-summary']").GetAttribute("data-interactive"));
        Assert.All(cut.FindAll("[data-testid='crmhr-account-summary'] button"), action => Assert.False(action.HasAttribute("disabled")));
        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();
        Assert.Single(intents);
    }

    private static CrmHrAccountSummary Summary()
        => new(
            AccountId,
            "Aurora Logistics",
            "Regional logistics account.",
            "Prospect",
            CrmHrAccountTone.Info,
            "Active",
            CrmHrAccountTone.Success,
            "accounts@aurora.example",
            "+1 555 0100",
            ["Customer", "Partner"],
            ConnectionCount: 3,
            OpportunityCount: 2,
            TagCount: 4,
            CanConvertToActiveCustomer: true);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
