using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class CatalogSelectionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_acceptance_retains_any_later_input_and_a_peer_observation_never_advances_the_original(bool sameValue) {
        var publication = Publication();
        var draft = new SharedProviderCatalogSelection(Origin(), "Owned source", [publication], [publication.PublicationId]);
        var submission = draft.Capture();
        draft.Select(publication.PublicationId, sameValue);
        var accepted = Origin(draft.Origin.SourceId);
        Assert.False(draft.Accept(submission, accepted));
        var peer = Origin(draft.Origin.SourceId);
        draft.Observe(peer);
        Assert.Equal(accepted, draft.Origin);
        Assert.Equal(sameValue, draft.IsSelected(publication.PublicationId));
        Assert.True(draft.ReadbackConflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selection_review_requires_an_explicit_whole_choice_and_only_adopts_the_reviewed_current(bool local) {
        var publication = Publication();
        var draft = new SharedProviderCatalogSelection(Origin(), "Owned source", [publication], []);
        draft.Select(publication.PublicationId, true);
        var current = Origin(draft.Origin.SourceId);
        draft.Compare(current, []);
        Assert.False(draft.ApplyComparison());
        if (local) {
            draft.Comparison!.Selection.UseLocal();
        } else {
            draft.Comparison!.Selection.UseCurrent();
        }
        Assert.True(draft.ApplyComparison());
        Assert.Equal(current, draft.Origin);
        Assert.Equal(local, draft.IsSelected(publication.PublicationId));
        Assert.True(draft.IsCurrent(draft.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_peer_or_same_value_checkbox_input_invalidates_a_selected_review(bool peer) {
        var publication = Publication();
        var draft = new SharedProviderCatalogSelection(Origin(), "Owned source", [publication], [publication.PublicationId]);
        var current = Origin(draft.Origin.SourceId);
        draft.Compare(current, []);
        draft.Comparison!.Selection.UseLocal();
        if (peer) {
            draft.Observe(Origin(draft.Origin.SourceId));
        } else {
            draft.Select(publication.PublicationId, true);
        }
        Assert.True(draft.Comparison.IsStale);
        Assert.False(draft.ApplyComparison());
    }

    private static SharedProviderSourceOrigin Origin(Guid? source = null) {
        var token = Guid.NewGuid();
        return new(Guid.NewGuid(), Guid.NewGuid(), source ?? Guid.NewGuid(), token) { Observation = new(token, []) };
    }

    private static SharedProviderCatalogPublication Publication() {
        var id = new SharedProviderPublicationId(Guid.NewGuid());
        var model = SharedProviderRoutingModelIdCodec.Create(id, "owned-model");
        return new(id, new($"sha256:{new string('a', 64)}"), "Owned selected provider", SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible, model, [new(model, "Owned model", [SharedProviderCapability.Responses])],
            new(SharedProviderHealthState.Available));
    }
}
