using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Operators.UI.Parties;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Operators.UI.Tests;

public sealed class PartyEditorTests {
    [Fact]
    public async Task Raw_input_is_submitted_without_blur_and_same_opening_keeps_its_edit_context() {
        await using var context = Context();
        var state = State();
        PartyQuickCreateInput? submitted = null;
        var cut = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, state)
            .Add(component => component.Create, input => { submitted = input; return Task.CompletedTask; }));
        var editContext = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.InvokeAsync(async () => {
            await cut.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new ChangeEventArgs { Value = "Unblurred name" });
            await cut.Find("[data-testid='project-structure-participant-quick-email']").InputAsync(new ChangeEventArgs { Value = "public@example.invalid" });
            await cut.Find("[data-testid='project-structure-participant-quick-phone']").InputAsync(new ChangeEventArgs { Value = "+0 123" });
            await cut.Find("[data-testid='project-structure-participant-quick-summary']").InputAsync(new ChangeEventArgs { Value = "Exact summary\nnext line" });
        });
        state.Message = "Options refreshed";
        cut.Render(parameters => parameters.Add(component => component.State, state));
        Assert.Same(editContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.True(editContext!.IsModified());
        await cut.InvokeAsync(() => cut.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click.InvokeAsync());
        Assert.Equal(new(PartyQuickCreateKind.Person, "Unblurred name", "public@example.invalid", "+0 123", "Exact summary\nnext line"), submitted);
    }

    [Fact]
    public async Task Direct_duplicate_callback_is_gated_and_retired_completion_cannot_release_successor() {
        await using var context = Context();
        var first = State();
        first.Draft.Name = "First";
        var second = State();
        second.Draft.Name = "Second";
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var cut = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, first)
            .Add(component => component.Create, input => {
                calls.Add(input.Name);
                return input.Name == "First" ? firstGate.Task : secondGate.Task;
            }));
        var firstCallback = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click;
        var firstPending = cut.InvokeAsync(() => firstCallback.InvokeAsync());
        await cut.InvokeAsync(() => firstCallback.InvokeAsync());
        Assert.Single(calls);
        cut.Render(parameters => parameters.Add(component => component.State, second));
        var secondCallback = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Create party").Instance.Click;
        var secondPending = cut.InvokeAsync(() => secondCallback.InvokeAsync());
        firstGate.SetResult();
        await firstPending;
        await cut.InvokeAsync(() => firstCallback.InvokeAsync());
        await cut.InvokeAsync(() => secondCallback.InvokeAsync());
        Assert.Equal(new[] { "First", "Second" }, calls);
        secondGate.SetResult();
        await secondPending;
    }

    [Fact]
    public async Task Meeting_defaults_and_missing_saved_identity_remain_explicit() {
        await using var context = Context();
        var retained = Guid.NewGuid();
        var standard = Guid.NewGuid();
        var state = new PartyEditorState(PartyEditorKind.Meeting, "Owned meeting") { IsLoading = false,
            Choices = [new(retained, "Missing identity", "Person", "link_off", false, IsMissing: true), new(standard, "Default", "Person", "person", true)],
            ProjectDefaults = [standard] };
        state.Draft.MeetingParties.Add(retained);
        PartySelection? submitted = null;
        var cut = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, state)
            .Add(component => component.Save, selection => { submitted = selection; return Task.CompletedTask; }));
        Assert.Contains("missing; retained", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.FindComponents<Button>().Single(button => button.Instance.Text == "Save meeting parties").Instance.Click.InvokeAsync());
        Assert.Equal(new[] { retained }, submitted!.MeetingParties);
        await cut.InvokeAsync(() => cut.FindComponents<Button>().Single(button => button.Instance.Text == "Use project defaults").Instance.Click.InvokeAsync());
        Assert.Equal(new[] { standard }, state.Draft.MeetingParties);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unavailable_and_successfully_empty_options_have_different_presentation(bool unavailable) {
        using var context = Context();
        var state = State();
        state.IsUnavailable = unavailable;
        var cut = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, state));
        Assert.Equal(unavailable, cut.Markup.Contains("could not be loaded", StringComparison.Ordinal));
        Assert.Equal(unavailable, cut.Find("[data-testid='project-structure-participant-save']").HasAttribute("disabled"));
    }

    [Fact]
    public void Sensitive_picker_never_renders_contacts_in_visible_hidden_or_search_data() {
        using var context = Context();
        var state = State();
        state.Choices = [new(Guid.NewGuid(), "Protected person", "Person", "person", true,
            "do-not-project@example.invalid", "hidden-phone-sentinel", IsSensitive: true)];
        var cut = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, state));
        Assert.DoesNotContain("do-not-project", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-phone-sentinel", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Contact details hidden", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_views_keep_raw_drafts_contexts_and_selection_independent() {
        await using var context = Context();
        var first = State();
        var second = State();
        var left = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, first));
        var right = context.Render<PartyEditor>(parameters => parameters.Add(component => component.State, second));
        await left.InvokeAsync(() => left.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new ChangeEventArgs { Value = "Only left" }));
        Assert.Equal("Only left", first.Draft.Name);
        Assert.Equal(string.Empty, second.Draft.Name);
        Assert.NotSame(left.FindComponent<EditForm>().Instance.EditContext, right.FindComponent<EditForm>().Instance.EditContext);
    }

    private static PartyEditorState State() => new(PartyEditorKind.Participant, "Participant") { IsLoading = false };
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
