using System.Reflection;
using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using CanDoItAll.Workspace.StorageSelection.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceStorageSelectionUi;

public sealed class SelectionLifetimeTests {
    private static readonly Guid Alpha = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beta = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Missing = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Equal_echo_and_local_search_preserve_staging_without_repeating_reads() {
        var source = new ControlledSource();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        IReadOnlyList<Guid>? result = null;
        var cut = Field(context, [Alpha], ids => result = ids);
        Assert.Equal(1, source.Reads);
        var opening = Open(cut);
        var option = host.WaitForElement($"[data-testid='field-dialog-option-{Beta:N}']");
        option.Click();
        cut.Render(parameters => parameters.Add(component => component.Value, [Guid.Empty, Alpha, Alpha]));
        host.Find("[data-testid='field-dialog-picker-search']").Input("Beta");
        Assert.Equal("true", host.Find($"[data-testid='field-dialog-option-{Beta:N}']").GetAttribute("aria-pressed"));
        Assert.Equal(2, source.Reads);
        Assert.Single(host.FindComponents<ResourceCardPicker<Guid>>());
        Assert.Single(cut.FindComponents<SelectedReferenceTable<Guid>>());
        host.Find("[data-testid='field-dialog-apply']").Click();
        await opening;
        Assert.Equal([Alpha, Beta], result);
        var reopened = Open(cut);
        host.WaitForElement("[data-testid='field-dialog-cancel']").Click();
        await reopened;
        Assert.Equal(3, source.Reads);
    }

    public enum Change { Selection, SelectionAwayAndBack, AllowAllAwayAndBack, DisabledAwayAndBack, ParentLifetime, Source }

    [Theory]
    [InlineData(Change.Selection)]
    [InlineData(Change.SelectionAwayAndBack)]
    [InlineData(Change.AllowAllAwayAndBack)]
    [InlineData(Change.DisabledAwayAndBack)]
    [InlineData(Change.ParentLifetime)]
    [InlineData(Change.Source)]
    public async Task Captured_dialog_cannot_apply_after_parent_or_source_intent_changes(Change change) {
        var source = new ControlledSource();
        using var context = CreateContext(source);
        using var firstOwner = new CancellationTokenSource();
        using var secondOwner = new CancellationTokenSource();
        var host = context.Render<DialogHost>();
        var calls = 0;
        var cut = Field(context, [Alpha], _ => calls++, firstOwner.Token);
        var opening = Open(cut);
        host.WaitForElement($"[data-testid='field-dialog-option-{Beta:N}']").Click();
        var service = context.Services.GetRequiredService<DialogService>();
        var original = Assert.Single(service.Dialogs);
        switch (change) {
            case Change.Selection:
            case Change.SelectionAwayAndBack:
                cut.Render(parameters => parameters.Add(component => component.Value, [Missing]));
                if (change == Change.SelectionAwayAndBack) {
                    cut.Render(parameters => parameters.Add(component => component.Value, [Alpha]));
                }
                break;
            case Change.AllowAllAwayAndBack:
                cut.Render(parameters => parameters.Add(component => component.AllowAll, true));
                cut.Render(parameters => parameters.Add(component => component.AllowAll, false));
                break;
            case Change.DisabledAwayAndBack:
                cut.Render(parameters => parameters.Add(component => component.Disabled, true));
                cut.Render(parameters => parameters.Add(component => component.Disabled, false));
                break;
            case Change.ParentLifetime:
                firstOwner.Cancel();
                cut.Render(parameters => parameters.Add(component => component.OwnerLifetime, secondOwner.Token));
                break;
            case Change.Source:
                await cut.InvokeAsync(source.Replace);
                break;
        }
        await cut.InvokeAsync(() => original.CloseAsync(new StorageCatalogSelectionDialogResult([Beta])));
        await opening;
        Assert.Empty(service.Dialogs);
        Assert.Equal(0, calls);
        Assert.DoesNotContain("field-dialog-shell", host.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_detail_completion_cleans_itself_without_replacing_successor(bool fail) {
        var source = new ControlledSource();
        var held = source.HoldNext();
        using var context = CreateContext(source);
        var cut = Field(context, [Alpha]);
        var oldRequest = Private<CancellationTokenSource>(cut.Instance, "detailsRequest");
        await cut.InvokeAsync(source.Replace);
        cut.WaitForAssertion(() => Assert.Contains("Profile 1", cut.Markup, StringComparison.Ordinal));
        Complete(held, fail);
        cut.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => oldRequest.Token));
        Assert.Contains("Profile 1", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-failure-sentinel", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("details-warning", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, source.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_read_finally_does_not_clear_new_busy_request(bool fail) {
        var source = new ControlledSource();
        var old = source.HoldNext();
        using var context = CreateContext(source);
        var cut = Field(context, [Alpha]);
        var oldRequest = Private<CancellationTokenSource>(cut.Instance, "detailsRequest");
        var current = source.HoldNext();
        var replacing = cut.InvokeAsync(source.Replace);
        await replacing;
        var currentRequest = Private<CancellationTokenSource>(cut.Instance, "detailsRequest");
        Assert.NotSame(oldRequest, currentRequest);
        Complete(old, fail);
        cut.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => oldRequest.Token));
        Assert.Same(currentRequest, Private<CancellationTokenSource>(cut.Instance, "detailsRequest"));
        Assert.Contains("Loading", cut.Markup, StringComparison.Ordinal);
        current.SetResult([Item(Alpha, "Current metadata")]);
        cut.WaitForAssertion(() => Assert.Contains("Current metadata", cut.Markup, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_dialog_read_and_catalog_callback_cannot_relabel_reopened_owner(bool fail) {
        var source = new ControlledSource();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        var cut = Field(context, [Alpha]);
        var held = source.HoldNext();
        var opening = Open(cut);
        host.WaitForElement("[data-testid='field-dialog-apply']");
        var oldDialog = host.FindComponent<StorageCatalogSelectionDialog>();
        var oldRequest = Private<CancellationTokenSource>(oldDialog.Instance, "readRequest");
        await cut.InvokeAsync(source.Replace);
        await opening;
        var reopened = Open(cut);
        host.WaitForElement($"[data-testid='field-dialog-option-{Alpha:N}']");
        Complete(held, fail);
        host.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => oldRequest.Token));
        Assert.Contains("Profile 1", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Profile 1", host.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-failure-sentinel", host.Markup, StringComparison.Ordinal);
        host.Find("[data-testid='field-dialog-cancel']").Click();
        await reopened;
    }

    [Fact]
    public async Task Handler_rejects_loading_failure_retirement_and_fabricated_ids() {
        var source = new ControlledSource();
        var held = source.HoldNext();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        var cut = Field(context, []);
        var opening = Open(cut);
        host.WaitForElement("[data-testid='field-dialog-apply']");
        var dialog = host.FindComponent<StorageCatalogSelectionDialog>();
        await Call(dialog, "ToggleCatalogAsync", Beta);
        await Call(dialog, "ConfirmAsync");
        Assert.False(opening.IsCompleted);
        held.SetException(new InvalidOperationException("private-failure-sentinel"));
        host.WaitForElement("[data-testid='field-dialog-retry']");
        await Call(dialog, "ConfirmAsync");
        Assert.False(opening.IsCompleted);
        host.Find("[data-testid='field-dialog-retry']").Click();
        host.WaitForElement($"[data-testid='field-dialog-option-{Alpha:N}']");
        await Call(dialog, "ToggleCatalogAsync", Missing);
        await Call(dialog, "ToggleCatalogAsync", Guid.Empty);
        Assert.Empty(Private<HashSet<Guid>>(dialog.Instance, "pendingSelectedIds"));
        var retired = dialog.Instance;
        await dialog.InvokeAsync(source.Replace);
        await cut.InvokeAsync(() => (Task)retired.GetType().GetMethod("ConfirmAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(retired, null)!);
        await opening;
        Assert.Empty(context.Services.GetRequiredService<DialogService>().Dialogs);
    }

    [Fact]
    public async Task Two_fields_dispose_only_their_owned_child_and_disposal_is_idempotent() {
        var source = new ControlledSource();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        var first = Field(context, []);
        var second = Field(context, [], testId: "second");
        var firstOpening = Open(first);
        host.WaitForElement("[data-testid='field-dialog-cancel']");
        var secondOpening = Open(second, "second");
        host.WaitForElement("[data-testid='second-dialog-cancel']");
        await first.InvokeAsync(async () => {
            await first.Instance.DisposeAsync();
            await first.Instance.DisposeAsync();
        });
        await firstOpening;
        var remaining = Assert.Single(context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.Equal("second-dialog-shell", remaining.Options.TestId);
        host.Find("[data-testid='second-dialog-cancel']").Click();
        await secondOpening;
        await context.DisposeRenderedComponentsAsync();
        Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public async Task Unavailable_details_are_distinct_from_successful_empty_and_retry_keeps_ids() {
        var source = new ControlledSource();
        var held = source.HoldNext();
        using var context = CreateContext(source);
        var cut = Field(context, [Alpha]);
        held.SetException(new InvalidOperationException("private-failure-sentinel"));
        cut.WaitForElement("[data-testid='field-details-retry']");
        Assert.DoesNotContain("Missing storage catalog", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-failure-sentinel", cut.Markup, StringComparison.Ordinal);
        source.Rows = [];
        cut.Find("[data-testid='field-details-retry']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Missing storage catalog", cut.Markup, StringComparison.Ordinal));
        Assert.Equal([Alpha], cut.Instance.Value);
        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public async Task Failed_refresh_retains_and_marks_prior_metadata_and_retry_preserves_staging() {
        var source = new ControlledSource();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        IReadOnlyList<Guid>? result = null;
        var cut = Field(context, [Alpha], ids => result = ids);
        var held = source.HoldNext();
        var opening = Open(cut);
        host.WaitForElement("[data-testid='field-dialog-apply']");
        held.SetException(new InvalidOperationException("private-failure-sentinel"));
        host.WaitForElement("[data-testid='field-dialog-retry']");
        Assert.Contains("Profile 0", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Previous catalog details", cut.Markup, StringComparison.Ordinal);
        host.Find("[data-testid='field-dialog-retry']").Click();
        host.WaitForElement($"[data-testid='field-dialog-option-{Beta:N}']").Click();
        Assert.Equal("true", host.Find($"[data-testid='field-dialog-option-{Beta:N}']").GetAttribute("aria-pressed"));
        host.Find("[data-testid='field-dialog-apply']").Click();
        await opening;
        Assert.Equal([Alpha, Beta], result);
        Assert.DoesNotContain("details-warning", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Field_rejects_direct_unacquired_or_fabricated_dialog_result(bool acquired) {
        var source = new ControlledSource();
        var held = acquired ? null : source.HoldNext();
        using var context = CreateContext(source);
        var host = context.Render<DialogHost>();
        var calls = 0;
        var cut = Field(context, [], _ => calls++);
        var opening = Open(cut);
        host.WaitForElement("[data-testid='field-dialog-apply']");
        var dialog = host.FindComponent<StorageCatalogSelectionDialog>();
        var request = acquired ? null : Private<CancellationTokenSource>(dialog.Instance, "readRequest");
        await cut.InvokeAsync(() => context.Services.GetRequiredService<DialogService>().Dialogs.Single().CloseAsync(new StorageCatalogSelectionDialogResult([Missing])));
        await opening;
        Assert.Equal(0, calls);
        if (held is not null) {
            held.SetResult([Item(Alpha, "Old")]);
            cut.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => request!.Token));
        }
    }

    private static Task Call(IRenderedComponent<StorageCatalogSelectionDialog> dialog, string method, params object[] arguments) => dialog.InvokeAsync(() =>
        (Task)dialog.Instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog.Instance, arguments)!);
    private static T Private<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static Task Open(IRenderedComponent<StorageCatalogSelectionField> cut, string testId = "field") =>
        cut.InvokeAsync(() => cut.Find($"[data-testid='{testId}-choose']").ClickAsync(new MouseEventArgs()));
    private static void Complete(TaskCompletionSource<IReadOnlyList<StorageSelectionItem>> held, bool fail) {
        if (fail) {
            held.SetException(new InvalidOperationException("private-failure-sentinel"));
        } else {
            held.SetResult([Item(Alpha, "Retired metadata")]);
        }
    }
    private static StorageSelectionItem Item(Guid id, string name) => new(id, name, "File system", "Local", "/fixture", 0, true, false, false, "Unknown");
    private static IRenderedComponent<StorageCatalogSelectionField> Field(BunitContext context, IReadOnlyList<Guid> ids,
        Action<IReadOnlyList<Guid>>? changed = null, CancellationToken lifetime = default, string testId = "field") => context.Render<StorageCatalogSelectionField>(parameters => parameters
            .Add(component => component.Value, ids).Add(component => component.OwnerLifetime, lifetime)
            .Add(component => component.ValueChanged, changed ?? (_ => { })).Add(component => component.DataTestId, testId));
    private static BunitContext CreateContext(ControlledSource source) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(source);
        return context;
    }

    private sealed class ControlledSource : IStorageCatalogSelectionSource {
        private Action? changed;
        private readonly Queue<TaskCompletionSource<IReadOnlyList<StorageSelectionItem>>> held = new();
        public StorageCatalogSelectionContext Context { get; private set; } = new(Guid.NewGuid(), 0);
        public bool IsCurrent => true;
        public int Reads { get; private set; }
        public int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public IReadOnlyList<StorageSelectionItem> Rows { get; set; } = [Item(Alpha, "Profile 0"), Item(Beta, "Beta")];
        public event Action? ContextChanged { add => changed += value; remove => changed -= value; }
        public TaskCompletionSource<IReadOnlyList<StorageSelectionItem>> HoldNext() {
            var request = new TaskCompletionSource<IReadOnlyList<StorageSelectionItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Enqueue(request);
            return request;
        }
        public void Replace() {
            Context = Context with { Generation = Context.Generation + 1 };
            Rows = [Item(Alpha, $"Profile {Context.Generation}"), Item(Beta, "Beta")];
            changed?.Invoke();
        }
        public Task<IReadOnlyList<StorageSelectionItem>> ListAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return held.TryDequeue(out var request) ? request.Task : Task.FromResult(Rows);
        }
    }
}
