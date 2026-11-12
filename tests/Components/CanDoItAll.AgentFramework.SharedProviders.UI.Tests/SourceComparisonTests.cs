using Bunit;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class SourceComparisonTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Comparison_preserves_context_and_unfinished_input_and_adopts_only_the_reviewed_current_original(bool incomplete) {
        var original = Values();
        var draft = new SharedProviderSourceDraft(Guid.NewGuid(), Guid.NewGuid(), original);
        var context = draft.Context;
        draft.Name = "Local source name";
        if (incomplete) {
            draft.BaseUri = "https://[unfinished";
        }
        var peer = original with { BaseUri = "https://peer.example.test/", CredentialReference = Guid.NewGuid(), AllowPrivateNetwork = true };
        var token = Guid.NewGuid();
        draft.Observe(token, peer);
        draft.Compare();
        Assert.False(draft.Comparison!.CanApply);
        if (incomplete) {
            draft.Comparison.KeepIncomplete = true;
        } else {
            draft.Comparison.Connection!.UseCurrent();
        }

        Assert.True(draft.ApplyComparison());

        Assert.Same(context, draft.Context);
        Assert.Equal("Local source name", draft.Name);
        Assert.Equal(incomplete ? "https://[unfinished" : peer.BaseUri, draft.BaseUri);
        Assert.Equal(peer, draft.Original!.Values);
        Assert.Equal(token, draft.ExpectedToken);
        Assert.Equal(!incomplete, draft.Context.Validate());
    }

    [Fact]
    public void Connection_credentials_and_network_access_require_a_complete_explicit_side_choice() {
        var draft = new SharedProviderSourceDraft(Guid.NewGuid(), Guid.NewGuid(), Values());
        draft.CredentialReference = Guid.NewGuid();
        var peer = Values() with { BaseUri = "https://another.example.test/", IsEnabled = false, AllowPrivateNetwork = true };
        draft.Observe(Guid.NewGuid(), peer);
        draft.Compare();
        Assert.False(draft.Comparison!.Connection!.IsResolved);
        draft.Comparison.Connection.UseCurrent();
        Assert.True(draft.ApplyComparison());
        Assert.Equal(peer.BaseUri, draft.BaseUri);
        Assert.Equal(peer.CredentialReference, draft.CredentialReference);
        Assert.Equal(peer.IsEnabled, draft.IsEnabled);
        Assert.Equal(peer.AllowPrivateNetwork, draft.AllowPrivateNetwork);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_newer_input_or_current_version_invalidates_the_open_review(bool peer) {
        var draft = new SharedProviderSourceDraft(Guid.NewGuid(), Guid.NewGuid(), Values());
        draft.Observe(Guid.NewGuid(), Values());
        draft.Compare();
        if (peer) {
            draft.Observe(Guid.NewGuid(), Values());
        } else {
            draft.Name = draft.Name;
        }
        Assert.True(draft.Comparison!.IsStale);
        Assert.False(draft.ApplyComparison());
        Assert.True(draft.ReadbackConflict);
    }

    [Fact]
    public void An_absent_original_has_explicit_current_review_without_a_fabricated_three_way_base() {
        var draft = new SharedProviderSourceDraft(Guid.NewGuid(), Guid.Empty, Values());
        draft.BaseUri = "invalid retained URL";
        draft.Observe(Guid.NewGuid(), Values() with { Name = "Saved peer" });
        draft.Compare();
        Assert.Null(draft.Comparison!.Name);
        Assert.False(draft.ApplyComparison());
        draft.Comparison.KeepWholeDraft = true;
        Assert.True(draft.ApplyComparison());
        Assert.Equal("invalid retained URL", draft.BaseUri);
        Assert.Equal("Saved peer", draft.Original!.Values.Name);
        Assert.False(draft.Context.Validate());
    }

    [Fact]
    public void Exact_acceptance_preserves_post_dispatch_input_and_remains_distinct_from_a_later_current_read() {
        var draft = new SharedProviderSourceDraft(Guid.NewGuid(), Guid.NewGuid(), Values());
        var submitted = draft.Capture();
        draft.Name = "Typed after dispatch";
        var accepted = Values() with { Name = "Normalized accepted" };
        var acceptedToken = Guid.NewGuid();
        Assert.False(draft.Accept(submitted, acceptedToken, accepted));
        draft.Observe(Guid.NewGuid(), Values() with { Name = "Later peer" });
        Assert.Equal("Typed after dispatch", draft.Name);
        Assert.Equal(acceptedToken, draft.Original!.Token);
        Assert.Equal(accepted, draft.Original.Values);
        Assert.True(draft.ReadbackConflict);
    }

    [Fact]
    public async Task Recovery_renderer_labels_the_actual_current_review_and_an_explicit_complete_draft_choice() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var review = new SharedProviderSourceComparison(null, Values() with { BaseUri = "https://user:private-value@example.test/?token=private-query" },
            new(Guid.NewGuid(), Values() with { Name = "Saved peer" }), false);
        var cut = context.Render<SharedProviderSourceComparisonSurface>(parameters => parameters.Add(component => component.Review, review));
        Assert.Contains("Saved peer", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("cannot establish the earlier write", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-value", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-query", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("label"), label => label.TextContent.Contains("Keep my complete draft against this reviewed source", StringComparison.Ordinal));
        Assert.False(review.CanApply);
        await cut.Find("input[type='checkbox']").ChangeAsync(new() { Value = true });
        Assert.True(review.CanApply);
        await context.DisposeRenderedComponentsAsync();
    }

    private static SharedProviderSourceValues Values() => new("Owned source", "https://original.example.test/",
        Guid.Parse("25bc6532-e87b-4126-85b3-561f933134fc"), true, false);
}
