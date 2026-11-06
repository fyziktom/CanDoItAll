using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class InteractionAppendStateTests {
    [Fact]
    public void Exact_acceptance_clears_the_same_form_model_before_follow_up_and_does_not_replay_the_entry() {
        var state = Open();
        var draft = state.Draft;
        draft.Subject = "  Submitted subject  ";
        var submitted = state.Begin()!;

        state.Accept(submitted, new(state.AccountPartyId, Guid.NewGuid(), new() { Subject = "Submitted subject" }));
        state.Complete(submitted);

        Assert.Same(draft, state.Draft);
        Assert.Empty(draft.Subject);
        Assert.NotNull(state.Accepted);
        Assert.False(state.CanSubmit);
        Assert.Null(state.Begin());
    }

    [Fact]
    public void Later_raw_and_ABA_input_remains_local_while_the_submitted_entry_is_frozen() {
        var state = Open();
        state.Draft.Subject = "Original raw subject";
        var submitted = state.Begin()!;
        state.Draft.Subject = "Later";
        state.Input();
        state.Draft.Subject = "Original raw subject";
        state.Input();
        state.Accept(submitted, new(state.AccountPartyId, Guid.NewGuid(), new() { Subject = "Original raw subject" }));
        state.Complete(submitted);

        Assert.Equal("Original raw subject", state.Draft.Subject);
        Assert.Equal("Original raw subject", submitted.Model.Subject);
        Assert.NotNull(state.Accepted);
        Assert.True(state.CanSubmit);
    }

    [Fact]
    public void Unknown_submission_survives_input_and_context_reads_without_authorizing_another_append() {
        var state = Open();
        state.Draft.Subject = "Unconfirmed journal";
        var submitted = state.Begin()!;
        state.Unknown(submitted);
        state.Complete(submitted);
        state.Draft.Notes = "  Later raw notes  ";
        state.Input();
        state.SetOpportunityContext(Guid.NewGuid());

        Assert.True(state.IsUnknown);
        Assert.Same(submitted, state.UnknownSubmission);
        Assert.False(state.CanSubmit);
        Assert.Null(state.Begin());
        Assert.Equal("  Later raw notes  ", state.Draft.Notes);
        Assert.Null(state.Draft.RelatedOpportunityId);
    }

    [Fact]
    public void An_old_account_callback_cannot_clear_a_reopened_same_account_draft() {
        var state = Open();
        state.Draft.Subject = "Old request";
        var submitted = state.Begin()!;
        state.Open(state.AccountPartyId, new());
        state.Draft.Subject = "Current draft";

        state.Accept(submitted, new(state.AccountPartyId, Guid.NewGuid(), new()));
        state.Unknown(submitted);
        state.Complete(submitted);

        Assert.Equal("Current draft", state.Draft.Subject);
        Assert.Null(state.Accepted);
        Assert.False(state.IsUnknown);
    }

    private static InteractionAppendState Open() {
        var state = new InteractionAppendState();
        state.Open(Guid.NewGuid(), new());
        return state;
    }
}
