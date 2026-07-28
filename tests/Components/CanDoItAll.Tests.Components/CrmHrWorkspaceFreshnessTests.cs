using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrWorkspaceFreshnessTests
{
    [Fact]
    public async Task Crm_opportunity_route_resolves_its_non_first_account_and_rejects_missing_or_conflicting_identity()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstAccountId = await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "A first account");
        var targetAccountId = await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "Z target account");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            "CRM route owner",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AccountManager);
        var opportunityResult = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = targetAccountId,
            Title = "Non-first account opportunity",
            Stage = OpportunityStage.Qualified,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            ProbabilityPercent = 40,
            LastChangedBy = "component-tests"
        });
        Assert.True(opportunityResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/crm?opportunityId={opportunityResult.Value:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();

        AssertCurrentCrmContext(
            cut,
            targetAccountId,
            "Z target account",
            opportunityResult.Value,
            "Non-first account opportunity");
        cut.WaitForElement("[data-testid='crmhr-opportunity-title']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-stage']"));

        navigation.NavigateTo(
            $"/crm-hr/crm?accountId={firstAccountId:D}&opportunityId={opportunityResult.Value:D}");
        AssertFailedCrmContext(cut);

        navigation.NavigateTo(
            $"/crm-hr/crm?accountId={targetAccountId:D}&opportunityId={Guid.NewGuid():D}");
        AssertFailedCrmContext(cut);
    }

    [Fact]
    public async Task Crm_explicit_opportunity_with_no_accounts_fails_closed()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/crm?opportunityId={Guid.NewGuid():D}");

        var cut = harness.Context.Render<CrmHrCrmPage>();

        AssertFailedCrmContext(cut);
    }

    [Fact]
    public async Task Crm_interaction_route_resolves_its_non_first_account_and_rejects_missing_identity()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "A first interaction account");
        var targetAccountId = await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "Z target interaction account");
        var interactionResult = await crmService.AddInteractionAsync(
            targetAccountId,
            new CrmInteractionEditorModel
            {
                InteractionType = InteractionType.Call,
                Subject = "Non-first account interaction",
                Summary = "Bounded route resolution proof"
            },
            "component-tests");
        Assert.True(interactionResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/crm?interactionId={interactionResult.Value:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();

        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<CrmAgentChatContextProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            var account = Assert.IsType<CrmAgentChatAccountContext>(contextProvider.Instance.Account);
            Assert.Equal(targetAccountId, account.AccountId);
            var interaction = Assert.IsType<CrmAgentChatInteractionContext>(contextProvider.Instance.Interaction);
            Assert.Equal(interactionResult.Value, interaction.InteractionId);
            Assert.Equal("Non-first account interaction", interaction.DisplayLabel);
            Assert.Equal(InteractionType.Call, interaction.InteractionType);
            Assert.Null(contextProvider.Instance.Opportunity);
        });
        cut.WaitForElement("[data-testid='crmhr-interaction-subject']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-stage']"));

        navigation.NavigateTo($"/crm-hr/crm?interactionId={Guid.NewGuid():D}");
        AssertFailedCrmContext(cut);
    }

    [Fact]
    public async Task Crm_late_account_load_and_explicit_missing_id_cannot_replace_current_context()
    {
        var loadGate = new DelayedDbContextCreationGate();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => WrapDbContextFactory(services, loadGate));
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstAccountId = await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "First delayed account");
        var secondAccountId = await CreateAccountAsync(
            partyDirectoryService,
            crmService,
            "Second current account");
        loadGate.Arm();

        navigation.NavigateTo($"/crm-hr/crm?accountId={firstAccountId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        await loadGate.WaitForDelayedCreationAsync();
        navigation.NavigateTo($"/crm-hr/crm?accountId={secondAccountId:D}");

        var renderCountBeforeRelease = cut.RenderCount;
        try
        {
            AssertCurrentCrmContext(cut, secondAccountId, "Second current account");
        }
        finally
        {
            loadGate.Release();
        }

        cut.WaitForAssertion(() => Assert.True(cut.RenderCount > renderCountBeforeRelease));
        AssertCurrentCrmContext(cut, secondAccountId, "Second current account");

        navigation.NavigateTo($"/crm-hr/crm?accountId={Guid.NewGuid():D}");
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<CrmAgentChatContextProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Account);
            Assert.Null(contextProvider.Instance.Opportunity);
        });
    }

    [Fact]
    public async Task Workforce_late_party_load_and_explicit_missing_id_cannot_replace_current_context()
    {
        var loadGate = new DelayedDbContextCreationGate();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => WrapDbContextFactory(services, loadGate));
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstPartyId = await CreateWorkerAsync(
            partyDirectoryService,
            hrService,
            "First delayed worker");
        var secondPartyId = await CreateWorkerAsync(
            partyDirectoryService,
            hrService,
            "Second current worker");
        loadGate.Arm();

        navigation.NavigateTo($"/crm-hr/workforce?partyId={firstPartyId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        await loadGate.WaitForDelayedCreationAsync();
        navigation.NavigateTo($"/crm-hr/workforce?partyId={secondPartyId:D}");

        var renderCountBeforeRelease = cut.RenderCount;
        try
        {
            AssertCurrentWorkforceContext(cut, secondPartyId, "Second current worker");
        }
        finally
        {
            loadGate.Release();
        }

        cut.WaitForAssertion(() => Assert.True(cut.RenderCount > renderCountBeforeRelease));
        AssertCurrentWorkforceContext(cut, secondPartyId, "Second current worker");

        navigation.NavigateTo($"/crm-hr/workforce?partyId={Guid.NewGuid():D}");
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-workforce-record-dialog']"));
        });
    }

    [Fact]
    public async Task Recruiting_query_selection_publishes_context_only_after_workspace_load_completes()
    {
        var loadGate = new DelayedDbContextCreationGate();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => WrapDbContextFactory(services, loadGate));
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruitingService = harness.Context.Services.GetRequiredService<RecruitingService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            "Delayed recruiting candidate",
            PartyType.Person,
            PartyLifecycleStatus.Candidate,
            PartyRoleKind.Candidate);
        var applicationResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                PartyId = candidateId,
                DesiredRole = "Platform engineer",
                Stage = RecruitmentStage.Interviewing,
                Decision = RecruitmentDecision.Pending,
                LastChangedBy = "component-tests"
            });
        Assert.True(applicationResult.IsSuccess);

        navigation.NavigateTo("/crm-hr/recruiting");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
        });

        loadGate.Arm();
        navigation.NavigateTo(
            $"/crm-hr/recruiting?applicationId={applicationResult.Value:D}");
        await loadGate.WaitForDelayedCreationAsync();
        try
        {
            cut.WaitForAssertion(() =>
            {
                var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
                Assert.Equal(
                    AgentChatContextAccessState.Loading,
                    contextProvider.Instance.ContextAccessState);
                Assert.Null(contextProvider.Instance.Surface.Position.PrimarySelection);
                Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
            });
        }
        finally
        {
            loadGate.Release();
        }

        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            var selection = Assert.IsType<AgentChatContextEntityReference>(
                contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Equal(applicationResult.Value.ToString("D"), selection.Id);
            Assert.Contains(
                contextProvider.Instance.Surface.Position.Facts,
                fact => fact.Name == "recruitment-stage" &&
                        fact.Value == RecruitmentStage.Interviewing.ToString());
            Assert.Contains(
                contextProvider.Instance.Surface.Position.Facts,
                fact => fact.Name == "decision-status" &&
                        fact.Value == RecruitmentDecision.Pending.ToString());
            Assert.Single(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
        });
    }

    [Fact]
    public async Task Workforce_party_without_profile_reports_unloaded_availability_until_allocations_are_requested()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Worker without profile",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee);

        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Not loaded",
                cut.Find("[data-testid='crmhr-workforce-summary-available']").TextContent.Trim());
            Assert.Equal(
                "Not loaded",
                cut.Find("[data-testid='crmhr-workforce-summary-next-availability']").TextContent.Trim());
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            var fact = Assert.Single(contextProvider.Instance.Surface.Position.Facts);
            Assert.Equal("lifecycle-status", fact.Name);
            Assert.Equal("Active", fact.Value);
        });

        cut.Find("[data-testid='crmhr-workforce-tab-allocations']").Click();
        cut.WaitForElement("[data-testid='crmhr-capacity-block-save-button']");
        cut.Find("[data-testid='crmhr-workforce-tab-overview']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "100%",
                cut.Find("[data-testid='crmhr-workforce-summary-available']").TextContent.Trim());
            Assert.Equal(
                "Not set",
                cut.Find("[data-testid='crmhr-workforce-summary-next-availability']").TextContent.Trim());
        });
    }

    private static void AssertCurrentCrmContext(
        IRenderedComponent<CrmHrCrmPage> cut,
        Guid expectedAccountId,
        string expectedDisplayName,
        Guid? expectedOpportunityId = null,
        string? expectedOpportunityName = null)
    {
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<CrmAgentChatContextProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            var account = Assert.IsType<CrmAgentChatAccountContext>(contextProvider.Instance.Account);
            Assert.Equal(expectedAccountId, account.AccountId);
            Assert.Equal(expectedDisplayName, account.DisplayLabel);
            Assert.Single(cut.FindAll("[data-testid='crmhr-crm-record-dialog']"));
            if (expectedOpportunityId.HasValue)
            {
                var opportunity = Assert.IsType<CrmAgentChatOpportunityContext>(
                    contextProvider.Instance.Opportunity);
                Assert.Equal(expectedOpportunityId.Value, opportunity.OpportunityId);
                Assert.Equal(expectedOpportunityName, opportunity.DisplayLabel);
            }
        });
    }

    private static void AssertFailedCrmContext(IRenderedComponent<CrmHrCrmPage> cut)
    {
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<CrmAgentChatContextProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Account);
            Assert.Null(contextProvider.Instance.Opportunity);
            Assert.Null(contextProvider.Instance.Interaction);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-crm-record-dialog']"));
        });
    }

    private static void AssertCurrentWorkforceContext(
        IRenderedComponent<CrmHrWorkforcePage> cut,
        Guid expectedPartyId,
        string expectedDisplayName)
    {
        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            var selection = Assert.IsType<AgentChatContextEntityReference>(
                contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Equal(expectedPartyId.ToString("D"), selection.Id);
            Assert.Equal(expectedDisplayName, selection.DisplayName);
            Assert.Single(cut.FindAll("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.Contains(expectedDisplayName, cut.Markup);
        });
    }

    private static async Task<Guid> CreateAccountAsync(
        PartyDirectoryService partyDirectoryService,
        CrmService crmService,
        string displayName)
    {
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            displayName,
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer);
        var profileResult = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = $"{displayName} commercial notes",
            LastChangedBy = "component-tests"
        });
        Assert.True(profileResult.IsSuccess);
        return accountId;
    }

    private static async Task<Guid> CreateWorkerAsync(
        PartyDirectoryService partyDirectoryService,
        HrService hrService,
        string displayName)
    {
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            displayName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee);
        var profileResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Freshness tester",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        });
        Assert.True(profileResult.IsSuccess);
        return partyId;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind)
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
            ]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static void WrapDbContextFactory(
        IServiceCollection services,
        DelayedDbContextCreationGate loadGate)
    {
        var factoryDescriptor = services.Last(descriptor =>
            descriptor.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(factoryDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider => new DelayedDbContextFactory(
                (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, factoryDescriptor),
                loadGate),
            factoryDescriptor.Lifetime));
    }

    private static object CreateService(
        IServiceProvider serviceProvider,
        ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(
                serviceProvider,
                descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Service descriptor for '{descriptor.ServiceType}' does not expose an implementation.");
    }

    private sealed class DelayedDbContextFactory(
        IDbContextFactory<AppDbContext> innerFactory,
        DelayedDbContextCreationGate loadGate) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => innerFactory.CreateDbContext();

        public async Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            var wasDelayed = await loadGate.WaitIfFirstArmedCreationAsync();
            return await innerFactory.CreateDbContextAsync(
                wasDelayed ? CancellationToken.None : cancellationToken);
        }
    }

    private sealed class DelayedDbContextCreationGate
    {
        private readonly TaskCompletionSource delayedCreation = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int isArmed;
        private int hasDelayedCreation;

        public void Arm()
            => Volatile.Write(ref isArmed, 1);

        public Task WaitForDelayedCreationAsync()
            => delayedCreation.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release()
            => release.TrySetResult();

        public async Task<bool> WaitIfFirstArmedCreationAsync()
        {
            if (Volatile.Read(ref isArmed) == 0 ||
                Interlocked.CompareExchange(ref hasDelayedCreation, 1, 0) != 0)
            {
                return false;
            }

            delayedCreation.TrySetResult();
            await release.Task;
            return true;
        }
    }
}
