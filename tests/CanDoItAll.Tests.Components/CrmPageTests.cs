using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmPageTests
{
    [Fact]
    public async Task Crm_page_saves_account_profile_stakeholders_and_interactions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Northwind Advisory",
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            "crm@northwind.example");
        var contactId = await CreatePartyAsync(
            partyDirectoryService,
            "Rina Billing",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.CustomerContact,
            "rina.billing@example.test");

        var cut = harness.Context.RenderComponent<CrmHrCrmPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Northwind Advisory", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-account-stage']").Change(CrmAccountRelationshipStage.ActiveCustomer.ToString());
        cut.Find("[data-testid='crmhr-account-commercial-notes']").Change("Renewal is likely if the pilot stays on track.");
        cut.Find("[data-testid='crmhr-account-constraints']").Change("Legal review required before expansion.");
        cut.Find("[data-testid='crmhr-account-timing-risks']").Change("Decision could slip by one month.");
        cut.Find("[data-testid='crmhr-account-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("CRM account profile saved.", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-stakeholder-add']").Click();
        cut.WaitForElement("[data-testid='crmhr-stakeholder-party-0']");
        cut.Find("[data-testid='crmhr-stakeholder-party-0']").Change(contactId.ToString());
        cut.Find("[data-testid='crmhr-stakeholder-role-0']").Change(CrmAccountStakeholderRole.BillingContact.ToString());
        cut.Find("[data-testid='crmhr-stakeholder-primary-0']").Change(true);
        cut.Find("[data-testid='crmhr-stakeholder-notes-0']").Change("Primary invoicing contact");
        cut.Find("[data-testid='crmhr-stakeholder-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Account stakeholders saved.", cut.Markup);
            Assert.Contains("Rina Billing", cut.Markup);
        });

        cut.Find($"[data-testid='crmhr-interaction-participant-{contactId:N}']").Change(true);
        cut.Find("[data-testid='crmhr-interaction-type']").Change(InteractionType.Call.ToString());
        cut.Find("[data-testid='crmhr-interaction-subject']").Change("Billing handoff call");
        cut.Find("[data-testid='crmhr-interaction-summary']").Change("Reviewed invoicing timeline and approval chain.");
        cut.Find("[data-testid='crmhr-next-action-text']").Change("Send draft statement of work");
        cut.Find("[data-testid='crmhr-next-action-owner']").Change(contactId.ToString());
        cut.Find("[data-testid='crmhr-next-action-due-on']").Change(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"));
        cut.Find("[data-testid='crmhr-interaction-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("CRM interaction saved.", cut.Markup);
            Assert.Contains("Billing handoff call", cut.Markup);
            Assert.Contains("Send draft statement of work", cut.Markup);
            Assert.Contains("Owner: Rina Billing", cut.Markup);
        });

        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, workspace.Profile.RelationshipStage);
        Assert.Single(workspace.Stakeholders);
        Assert.Single(workspace.OverdueNextActions);
        Assert.Contains(
            workspace.ActivityTimeline,
            item => item.Title.Contains("Billing handoff call", StringComparison.Ordinal));
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
