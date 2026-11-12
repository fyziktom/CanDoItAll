using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Workbench.Operators.UI.Secrets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Operators.UI.Tests;

public sealed class SecretReferenceEditorTests {
    [Fact]
    public async Task Unblurred_sensitive_input_is_captured_once_and_callback_copy_is_cleared() {
        await using var context = Context();
        var state = State();
        var value = Guid.NewGuid().ToString("N");
        SecretEditorModel? captured = null;
        SecretReferenceInput? reference = null;
        var matched = false;
        var cut = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, state)
            .Add(component => component.Create, (input, model) => {
                captured = model;
                reference = input;
                matched = model.SecretValue == value;
                return Task.CompletedTask;
            }));
        await cut.InvokeAsync(async () => {
            await cut.Find("[data-testid='project-structure-secret-create-name']").InputAsync(new() { Value = "Exact new name" });
            await cut.Find("[data-testid='project-structure-secret-purpose']").InputAsync(new() { Value = "Original purpose" });
            await cut.Find("input[data-testid='project-structure-secret-create-value']").InputAsync(new() { Value = value });
        });
        var edit = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Render(parameters => parameters.Add(component => component.State, state));
        Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.True(edit!.IsModified());
        await cut.InvokeAsync(() => Create(cut).InvokeAsync());
        Assert.True(matched);
        Assert.Equal("Exact new name", captured!.Name);
        Assert.Equal("Original purpose", reference!.Purpose);
        Assert.Empty(captured.SecretValue);
        state.Retire();
        cut.Render(parameters => parameters.Add(component => component.State, state));
        Assert.False(cut.Markup.Contains(value, StringComparison.Ordinal));
        Assert.All(cut.FindComponents<CopyButton>(), button => Assert.False(button.Instance.Value == value));
    }

    [Fact]
    public async Task Direct_duplicate_and_stale_callbacks_cannot_submit_or_release_a_successor() {
        await using var context = Context();
        var first = State();
        var second = State();
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, first)
            .Add(component => component.Create, (_, _) => ++calls == 1 ? firstGate.Task : secondGate.Task));
        var oldCallback = Create(cut);
        var original = cut.InvokeAsync(() => oldCallback.InvokeAsync());
        await cut.InvokeAsync(() => oldCallback.InvokeAsync());
        Assert.Equal(1, calls);
        first.Retire();
        cut.Render(parameters => parameters.Add(component => component.State, second));
        var nextCallback = Create(cut);
        var successor = cut.InvokeAsync(() => nextCallback.InvokeAsync());
        firstGate.SetResult();
        await original;
        await cut.InvokeAsync(() => oldCallback.InvokeAsync());
        await cut.InvokeAsync(() => nextCallback.InvokeAsync());
        Assert.Equal(2, calls);
        secondGate.SetResult();
        await successor;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_and_unavailable_metadata_remain_distinct(bool unavailable) {
        using var context = Context();
        var state = State();
        state.IsUnavailable = unavailable;
        state.Message = unavailable ? "Metadata unavailable" : string.Empty;
        var cut = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, state));
        Assert.Equal(!unavailable, cut.Markup.Contains("No stored secrets are available", StringComparison.Ordinal));
        Assert.Equal(unavailable, cut.Find("[data-testid='project-structure-secret-create-use']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Search_preserves_selected_identity_and_missing_reference_is_explicit() {
        await using var context = Context();
        var state = State();
        var id = Guid.NewGuid();
        state.Items = [new(id, "Same name", SecretKind.Token, "Owned scope", DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), "Same name", SecretKind.Token, "Other scope", DateTimeOffset.UtcNow)];
        state.Draft.SelectedId = id;
        var cut = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, state));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-secret-search']").InputAsync(new() { Value = "No match" }));
        Assert.Single(cut.FindAll($"option[value='{id:D}']"));
        state.Items = [];
        cut.Render(parameters => parameters.Add(component => component.State, state));
        Assert.Contains("Unavailable saved secret", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(id, state.Draft.SelectedId);
        Assert.True(cut.Find("[data-testid='project-structure-secret-use-selected']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Two_editors_keep_separate_sensitive_drafts_and_retirement_clears_only_its_owner() {
        await using var context = Context();
        var first = State();
        var second = State();
        var value = Guid.NewGuid().ToString("N");
        var left = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, first));
        var right = context.Render<SecretReferenceEditor>(parameters => parameters.Add(component => component.State, second));
        await left.InvokeAsync(() => left.Find("input[data-testid='project-structure-secret-create-value']").InputAsync(new() { Value = value }));
        Assert.Empty(second.Draft.Create.SecretValue);
        second.Retire();
        Assert.True(first.Draft.Create.SecretValue == value);
        first.Retire();
        left.Render();
        Assert.False(left.Markup.Contains(value, StringComparison.Ordinal));
        Assert.NotSame(left.FindComponent<EditForm>().Instance.EditContext, right.FindComponent<EditForm>().Instance.EditContext);
    }

    private static EventCallback Create(IRenderedComponent<SecretReferenceEditor> cut)
        => cut.FindComponents<Button>().Single(button => button.Instance.Text == "Create and use").Instance.Click;
    private static SecretReferenceState State() => new(false) { IsLoading = false };
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
