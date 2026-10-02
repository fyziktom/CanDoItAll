using Bunit;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class SharingRendererTests {
    [Fact]
    public async Task Actual_publication_child_and_confirmation_render_without_runtime_services() {
        using var context = Context();
        var view = new View();
        var cut = context.Render<SharedProviderSharingSurface>(p => p.Add(c => c.View, view));
        Assert.Single(cut.FindComponents<SharedProviderLocalPublicationContent>());
        await cut.Find("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.Equal(view.Presentation.Origin, view.Published);
        view.Presentation = view.Presentation with {
            Profile = view.Presentation.Profile! with { Publication = new(new(Guid.NewGuid()), true) }
        };
        cut.Render();
        await cut.Find("[data-testid='shared-provider-unpublish']").ClickAsync();
        var confirmation = Assert.IsType<SharedProviderConfirmation>(view.Presentation.Confirmation);
        Assert.NotNull(cut.Find("[data-testid='shared-provider-confirmation-dialog']"));
        await cut.Find("[data-testid='shared-provider-confirmation-apply']").ClickAsync();
        Assert.Equal(confirmation, view.Confirmed);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Actual_import_child_captures_unblurred_input_and_keeps_its_validation_context() {
        using var context = Context();
        var view = new View();
        var baseline = new SharedProviderImportBaseline(Guid.NewGuid(), view.Presentation.Origin.ProviderId!.Value,
            Guid.NewGuid(), new(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), new("Saved alias", true));
        var draft = new SharedProviderImportDraft(baseline);
        var model = SharedProviderRoutingModelIdCodec.Create(baseline.PublicationId, "native model 東京");
        view.Presentation = view.Presentation with {
            Draft = draft,
            Origin = view.Presentation.Origin with { Import = baseline },
            Profile = new(SharedProviderOwnership.Imported, null, null, new(baseline, "Source", "Public name",
                SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible, model, true,
                SharedProviderImportAvailability.Available, [new(model, "native model 東京", [SharedProviderCapability.Responses])]))
        };
        var cut = context.Render<SharedProviderSharingSurface>(p => p.Add(c => c.View, view));
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        cut.Find("[data-testid='shared-provider-import-alias']").Input(" ");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Null(view.Submitted);
        Assert.Contains("Enter a local alias.", cut.Markup, StringComparison.Ordinal);
        var editContext = draft.Context;
        draft.Reconcile(baseline with { ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid() });
        cut.Render();
        Assert.Same(child, cut.FindComponent<SharedProviderImportedProfileContent>().Instance);
        Assert.Same(editContext, draft.Context);
        Assert.Contains("Enter a local alias.", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='shared-provider-import-alias']").Input("  Typed without blur  ");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Equal("  Typed without blur  ", view.Submitted!.Settings.LocalAlias);
        cut.Find("[data-testid='shared-provider-import-alias']").Input("Later edit");
        Assert.Equal("  Typed without blur  ", view.Submitted.Settings.LocalAlias);
        Assert.Contains("native model 東京", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(model.Value, cut.Markup, StringComparison.Ordinal);
        await context.DisposeRenderedComponentsAsync();
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed class View : ISharedProviderSharingView {
        public SharedProviderSharingPresentation Presentation { get; set; } = new(
            new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), null, null),
            new(SharedProviderOwnership.Local, null, new(true, "Eligible saved provider",
                [new("Native display model", [SharedProviderCapability.Responses])]), null), null, false, false, null, null);
        public SharedProviderSharingOrigin? Published { get; private set; }
        public SharedProviderImportSubmission? Submitted { get; private set; }
        public SharedProviderConfirmation? Confirmed { get; private set; }
        public Task RetryAsync(SharedProviderSharingOrigin origin) => Task.CompletedTask;
        public Task PublishAsync(SharedProviderSharingOrigin origin) {
            Published = origin;
            return Task.CompletedTask;
        }
        public Task SaveImportedAsync(SharedProviderSharingOrigin origin, SharedProviderImportSubmission submission) {
            Submitted = submission;
            return Task.CompletedTask;
        }
        public void OpenConfirmation(SharedProviderSharingOrigin origin, SharedProviderConfirmationKind kind) =>
            Presentation = Presentation with { Confirmation = new(Guid.NewGuid(), origin, kind) };
        public Task ConfirmAsync(SharedProviderConfirmation confirmation) {
            Confirmed = confirmation;
            return Task.CompletedTask;
        }
        public void CloseConfirmation(SharedProviderConfirmation confirmation) => Presentation = Presentation with { Confirmation = null };
    }
}
