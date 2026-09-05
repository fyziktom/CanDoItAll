using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderPublicationPanelTests
{
    [Fact]
    public async Task Eligible_local_provider_can_be_published_from_provider_settings()
    {
        var providerId = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            CreateLocalState(providerId, isPublished: false, isEligible: true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId));

        cut.WaitForElement("[data-testid='shared-provider-publish']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(SharedProviderPublicationAction.Publish, service.PublicationAction);
            Assert.Contains("Published", cut.Find("[data-testid='shared-provider-publication-status']").TextContent);
        });
    }

    [Fact]
    public async Task Ineligible_local_provider_explains_why_publish_is_disabled()
    {
        var providerId = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            CreateLocalState(providerId, isPublished: false, isEligible: false));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId));

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
            Assert.Contains(
                "enabled before it can be published",
                cut.Find("[data-testid='shared-provider-eligibility-reason']").TextContent,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Published_provider_requires_confirmation_before_unpublish()
    {
        var providerId = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            CreateLocalState(providerId, isPublished: true, isEligible: true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId));

        cut.WaitForElement("[data-testid='shared-provider-unpublish']").Click();
        cut.WaitForElement("[data-testid='shared-provider-confirmation-dialog']");
        Assert.Null(service.PublicationAction);

        cut.Find("[data-testid='shared-provider-confirmation-apply']").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal(SharedProviderPublicationAction.Unpublish, service.PublicationAction));
    }

    internal static SharedProviderProfileSharingSnapshot CreateLocalState(
        Guid providerId,
        bool isPublished,
        bool isEligible)
    {
        var publicId = new SharedProviderPublicationId(Guid.NewGuid());
        var eligibility = isEligible
            ? new SharedProviderPublicationEligibility(
                SharedProviderPublicationEligibilityCode.Eligible,
                "The provider profile is eligible for publication.",
                SharedProviderPurpose.Chat,
                SharedProviderTransport.OpenAiCompatible,
                [new SharedProviderEligibleModel("gpt-test", [SharedProviderCapability.Responses])])
            : new SharedProviderPublicationEligibility(
                SharedProviderPublicationEligibilityCode.ProfileDisabled,
                "The provider profile must be enabled before it can be published.",
                Purpose: null,
                Transport: null,
                Models: []);
        return new SharedProviderProfileSharingSnapshot(
            providerId,
            SharedProviderProfileOwnership.Local,
            new SharedProviderPublicationWriteResult(
                Guid.NewGuid(),
                publicId,
                isPublished,
                Guid.NewGuid()),
            eligibility,
            Import: null);
    }
}

internal sealed class RecordingSharedProviderManagementService(
    SharedProviderProfileSharingSnapshot initialState,
    IReadOnlyList<SharedProviderSourceManagementSnapshot>? sourceSnapshots = null)
    : ISharedProviderManagementService
{
    public SharedProviderProfileSharingSnapshot State { get; private set; } = initialState;

    public IReadOnlyList<SharedProviderSourceManagementSnapshot> Sources { get; } = sourceSnapshots ?? [];

    public SharedProviderPublicationAction? PublicationAction { get; private set; }

    public SharedProviderImportedProfileUpdateRequest? ImportedUpdate { get; private set; }

    public Task<SharedProviderProfileSharingSnapshot> GetProfileSharingAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(State);

    public Task<SharedProviderProfileSharingSnapshot> SetPublicationAsync(
        Guid providerProfileId,
        SharedProviderPublicationAction action,
        Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        PublicationAction = action;
        State = State with
        {
            Publication = State.Publication! with
            {
                IsPublished = action == SharedProviderPublicationAction.Publish,
                ConcurrencyToken = Guid.NewGuid()
            }
        };
        return Task.FromResult(State);
    }

    public int ListSourcesCallCount { get; private set; }

    public Task<IReadOnlyList<SharedProviderSourceManagementSnapshot>> ListSourcesAsync(
        CancellationToken cancellationToken = default) {
        ListSourcesCallCount++;
        return Task.FromResult(Sources);
    }

    public Task<SharedProviderSourceWriteResult> SaveSourceAsync(
        SharedProviderSourceEditorRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SharedProviderSourceWriteResult> SetSourceEnabledAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        bool isEnabled,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SharedProviderSourceDeleteResult> DeleteSourceAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SharedProviderSourceOperationResult> TestSourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SharedProviderSourceOperationResult> SynchronizeSourceAsync(
        Guid sourceId,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<SharedProviderProfileSharingSnapshot> UpdateImportedProfileAsync(
        SharedProviderImportedProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ImportedUpdate = request;
        State = State with
        {
            Import = State.Import! with
            {
                LocalAlias = request.LocalAlias,
                IsEnabled = request.IsEnabled,
                ImportConcurrencyToken = Guid.NewGuid(),
                ProviderConcurrencyToken = Guid.NewGuid()
            }
        };
        return Task.FromResult(State);
    }

    public Task<SharedProviderProfileSharingSnapshot> RetireImportedProfileAsync(
        SharedProviderImportedProfileRetireRequest request,
        CancellationToken cancellationToken = default)
    {
        State = State with
        {
            Import = State.Import! with
            {
                SelectionState = SharedProviderSelectionState.Retired,
                ImportConcurrencyToken = Guid.NewGuid()
            }
        };
        return Task.FromResult(State);
    }
}
