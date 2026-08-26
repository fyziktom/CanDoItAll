using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderSourceAndImportComponentTests
{
    [Fact]
    public async Task Imported_profile_exposes_only_local_alias_and_enabled_intent_as_inputs()
    {
        var providerId = Guid.NewGuid();
        var import = CreateImport(providerId);
        var state = new SharedProviderProfileSharingSnapshot(
            providerId,
            SharedProviderProfileOwnership.Imported,
            Publication: null,
            Eligibility: null,
            import);
        var service = new RecordingSharedProviderManagementService(state);
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId));

        var aliasInput = (IHtmlInputElement)cut.WaitForElement("[data-testid='shared-provider-import-alias']");
        aliasInput.Change("Local finance model");
        cut.Find("[data-testid='shared-provider-import-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Local finance model", service.ImportedUpdate?.LocalAlias);
            Assert.DoesNotContain("providers-base-url-input", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("providers-api-key-input", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(import.RemoteDisplayName, cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Source_dialog_lists_secret_names_but_never_secret_values()
    {
        var providerId = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(
                providerId,
                isPublished: false,
                isEligible: true));
        var secret = new SecretListItem(
            Guid.NewGuid(),
            "Remote instance JWT",
            SecretKind.ApiKey,
            "workspace",
            DateTimeOffset.UtcNow);
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId)
            .Add(component => component.Secrets, [secret]));

        cut.WaitForElement("[data-testid='shared-provider-source-add']").Click();
        cut.WaitForElement("[data-testid='shared-provider-source-dialog']");

        Assert.Contains(secret.Name, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("component-test-secret", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretValue", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_list_shows_offline_state_as_unavailable_and_keeps_recovery_actions()
    {
        var providerId = Guid.NewGuid();
        var source = new SharedProviderSourceSnapshot(
            Guid.NewGuid(),
            "Office network instance",
            new Uri("http://192.168.10.12:5032/"),
            Guid.NewGuid(),
            true,
            SharedProviderSourceNetworkPolicy.AllowPrivateNetwork,
            SharedProviderSourceStatus.SourceOffline,
            RemoteInstanceId: null,
            LastCatalogETag: null,
            LastSyncAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5),
            LastStatusCode: 503,
            LastStatusMessage: "The source could not be reached.",
            ConcurrencyToken: Guid.NewGuid());
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(
                providerId,
                isPublished: false,
                isEligible: true),
            [new SharedProviderSourceManagementSnapshot(source, [])]);
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));

        var cut = harness.Context.Render<SharedProviderManagementPanel>(parameters => parameters
            .Add(component => component.ProviderProfileId, providerId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("source offline", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("The source could not be reached.", cut.Markup, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='shared-provider-source-test']"));
            Assert.NotNull(cut.Find("[data-testid='shared-provider-source-discover']"));
        });
    }

    private static SharedProviderImportedProfileSnapshot CreateImport(Guid providerId)
    {
        var publicationId = new SharedProviderPublicationId(Guid.NewGuid());
        return new SharedProviderImportedProfileSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Central CanDoItAll",
            publicationId,
            providerId,
            "Local alias",
            true,
            "Remote provider display name",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            SharedProviderRoutingModelIdCodec.Create(publicationId, "remote-model"),
            SharedProviderSelectionState.Selected,
            SharedProviderAvailabilityState.Available,
            [],
            Guid.NewGuid(),
            Guid.NewGuid());
    }
}
