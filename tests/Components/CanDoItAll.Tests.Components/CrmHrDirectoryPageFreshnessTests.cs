using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrDirectoryPageFreshnessTests
{
    [Fact]
    public async Task Saving_profile_without_opening_relationships_preserves_existing_relationships()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var managementService = harness.Context.Services.GetRequiredService<PartyDirectoryManagementService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var sourcePartyId = await CreatePartyAsync(partyDirectoryService, "Profile save source");
        var targetPartyId = await CreatePartyAsync(partyDirectoryService, "Profile save target");
        var relationshipSave = await managementService.SaveRelationshipsAsync(
            sourcePartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = targetPartyId,
                    RelationshipKind = PartyRelationshipKind.ReportsTo,
                    IsOutgoing = true,
                    IsPrimary = true,
                    Notes = "Existing reporting relationship"
                }
            ],
            "component-tests");
        Assert.True(relationshipSave.IsSuccess);

        navigation.NavigateTo($"/crm-hr/directory?partyId={sourcePartyId:D}");
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Profile save source",
                cut.Find("[data-testid='crmhr-party-display-name']").GetAttribute("value"));
            Assert.Equal(
                "true",
                cut.Find("[data-testid='crmhr-directory-tab-profile']").GetAttribute("aria-selected"));
        });
        cut.WaitForElement("[data-testid='crmhr-party-summary']")
            .Change("Updated from the default profile tab");

        await cut.Find("[data-testid='crmhr-party-save-button']")
            .ClickAsync(new MouseEventArgs());

        var savedParty = await partyDirectoryService.GetPartyAsync(sourcePartyId);
        var preservedRelationship = Assert.Single(
            await managementService.ListRelationshipsAsync(sourcePartyId));

        Assert.NotNull(savedParty);
        Assert.Equal("Updated from the default profile tab", savedParty.Summary);
        Assert.Equal(targetPartyId, preservedRelationship.RelatedPartyId);
        Assert.Equal(PartyRelationshipKind.ReportsTo, preservedRelationship.RelationshipKind);
        Assert.True(preservedRelationship.IsOutgoing);
        Assert.True(preservedRelationship.IsPrimary);
        Assert.Equal("Existing reporting relationship", preservedRelationship.Notes);
    }

    [Fact]
    public async Task Directory_paging_limits_the_slice_and_search_returns_to_the_first_page()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        for (var index = 1; index <= 10; index++)
        {
            await CreatePartyAsync(partyDirectoryService, $"Paging slice party {index:D2}");
        }

        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();
        cut.WaitForElement("[data-testid='crmhr-directory-search']")
            .Input("Paging slice");

        cut.WaitForAssertion(() =>
        {
            var scrollOwner = Assert.Single(
                cut.FindAll("[data-testid='crmhr-directory-list-scroll']"));
            Assert.Equal(8, cut.FindAll("[data-testid='crmhr-directory-item']").Count);
            Assert.Contains("Paging slice party 01", cut.Markup);
            Assert.Contains("Paging slice party 08", cut.Markup);
            Assert.DoesNotContain("Paging slice party 09", cut.Markup);
            Assert.Contains("Showing 1-8 of 10", cut.Markup);
            Assert.Contains(
                "overflow-y: auto",
                scrollOwner.GetAttribute("style") ?? string.Empty);
            Assert.Null(scrollOwner.QuerySelector("[data-testid='crmhr-directory-pager']"));
        });

        cut.Find("[data-testid='crmhr-directory-next-page']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("[data-testid='crmhr-directory-item']").Count);
            Assert.DoesNotContain("Paging slice party 08", cut.Markup);
            Assert.Contains("Paging slice party 09", cut.Markup);
            Assert.Contains("Paging slice party 10", cut.Markup);
            Assert.Contains("Showing 9-10 of 10", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-directory-search']")
            .Input("Paging slice party");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(8, cut.FindAll("[data-testid='crmhr-directory-item']").Count);
            Assert.Contains("Paging slice party 01", cut.Markup);
            Assert.Contains("Paging slice party 08", cut.Markup);
            Assert.DoesNotContain("Paging slice party 09", cut.Markup);
            Assert.Contains("Showing 1-8 of 10", cut.Markup);
        });
    }

    [Fact]
    public async Task Late_previous_party_load_cannot_replace_current_agent_chat_surface()
    {
        var loadGate = new DelayedDbContextCreationGate();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => WrapDbContextFactory(services, loadGate));
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var firstPartyId = await CreatePartyAsync(partyDirectoryService, "First delayed party");
        var secondPartyId = await CreatePartyAsync(partyDirectoryService, "Second current party");
        loadGate.Arm();

        navigation.NavigateTo($"/crm-hr/directory?partyId={firstPartyId:D}");
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();
        await loadGate.WaitForDelayedCreationAsync();
        navigation.NavigateTo($"/crm-hr/directory?partyId={secondPartyId:D}");

        var renderCountBeforeRelease = cut.RenderCount;
        try
        {
            AssertCurrentAgentChatSurface(cut, secondPartyId, "Second current party");
        }
        finally
        {
            loadGate.Release();
        }

        cut.WaitForAssertion(() => Assert.True(cut.RenderCount > renderCountBeforeRelease));
        AssertCurrentAgentChatSurface(cut, secondPartyId, "Second current party");
    }

    [Fact]
    public async Task Explicit_missing_party_never_publishes_the_previous_party_as_current()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var existingPartyId = await CreatePartyAsync(partyDirectoryService, "Existing party");
        var missingPartyId = Guid.NewGuid();
        navigation.NavigateTo($"/crm-hr/directory?partyId={existingPartyId:D}");
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();
        AssertCurrentAgentChatSurface(cut, existingPartyId, "Existing party");

        navigation.NavigateTo($"/crm-hr/directory?partyId={missingPartyId:D}");

        cut.WaitForAssertion(() =>
        {
            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Failed,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.DoesNotContain(existingPartyId.ToString("D"), contextProvider.Instance.Surface.Source.Id.Value, StringComparison.Ordinal);
        });
    }

    private static void AssertCurrentAgentChatSurface(
        IRenderedComponent<CrmHrDirectoryPage> cut,
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
        });
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests"
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
            await loadGate.WaitIfFirstArmedCreationAsync(cancellationToken);
            return await innerFactory.CreateDbContextAsync(cancellationToken);
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

        public async Task WaitIfFirstArmedCreationAsync(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref isArmed) == 0 ||
                Interlocked.CompareExchange(ref hasDelayedCreation, 1, 0) != 0)
            {
                return;
            }

            delayedCreation.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }
    }
}
