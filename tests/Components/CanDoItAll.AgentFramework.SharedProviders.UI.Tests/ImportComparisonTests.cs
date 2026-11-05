using CanDoItAll.AgentFramework.SharedProviders.UI;
using Xunit;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class ImportComparisonTests {
    [Fact]
    public void A_review_preserves_the_local_alias_and_takes_the_peers_independent_enabled_change_without_saving() {
        var original = Original();
        var draft = new SharedProviderImportDraft(original) { LocalAlias = "My unfinished alias" };
        var context = draft.Context;
        var current = original with { ImportToken = Guid.NewGuid(), ProviderToken = Guid.NewGuid(), Settings = new("Peer alias", false) };
        draft.Reconcile(current);
        draft.Compare();
        var review = Assert.IsType<SharedProviderImportComparison>(draft.Comparison);
        Assert.Equal("Original alias", review.Alias.Original);
        Assert.Equal("My unfinished alias", review.Alias.Local);
        Assert.Equal("Peer alias", review.Alias.Current);
        Assert.False(review.CanApply);
        review.Alias.UseLocal();
        Assert.True(draft.ApplyComparison());
        Assert.Same(context, draft.Context);
        Assert.Equal("My unfinished alias", draft.LocalAlias);
        Assert.False(draft.IsEnabled);
        Assert.Equal(current.ProviderToken, draft.Capture().Baseline.ProviderToken);
        Assert.True(draft.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_new_hint_or_same_value_input_invalidates_review(bool hint) {
        var original = Original();
        var draft = new SharedProviderImportDraft(original) { LocalAlias = "My alias" };
        draft.Reconcile(original with { ImportToken = Guid.NewGuid(), Settings = new("Peer alias", false) });
        draft.Compare();
        var review = draft.Comparison!;
        review.Alias.UseLocal();
        if (hint) {
            draft.Invalidate();
        } else {
            draft.LocalAlias = draft.LocalAlias;
        }
        Assert.True(review.IsStale);
        Assert.False(draft.ApplyComparison());
        Assert.Equal(original.ProviderToken, draft.Baseline.ProviderToken);
    }

    [Fact]
    public void Invalid_raw_alias_survives_review_and_remains_invalid_for_a_separate_save() {
        var original = Original();
        var draft = new SharedProviderImportDraft(original) { LocalAlias = " " };
        draft.Reconcile(original with { ProviderToken = Guid.NewGuid(), Settings = new("Peer alias", false) });
        draft.Compare();
        var review = draft.Comparison!;
        Assert.False(draft.ApplyComparison());
        review.KeepIncomplete = true;
        Assert.True(draft.ApplyComparison());
        Assert.Equal(" ", draft.LocalAlias);
        Assert.False(draft.CanSubmit(draft.Capture()));
    }

    [Fact]
    public void Exact_accepted_state_advances_the_original_without_erasing_post_dispatch_input() {
        var original = Original();
        var draft = new SharedProviderImportDraft(original) { LocalAlias = "  Sent alias  " };
        var submitted = draft.Capture();
        draft.LocalAlias = "  Sent alias  ";
        draft.Accept(submitted, original with { ProviderToken = Guid.NewGuid(), Settings = new("Sent alias", true) });
        Assert.Equal("  Sent alias  ", draft.LocalAlias);
        Assert.Equal("Sent alias", draft.Baseline.Settings.LocalAlias);
        Assert.True(draft.IsDirty);
    }

    private static SharedProviderImportBaseline Original() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid()),
        Guid.NewGuid(), Guid.NewGuid(), new("Original alias", true));
}
