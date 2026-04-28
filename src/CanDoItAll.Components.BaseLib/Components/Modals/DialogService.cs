using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CanDoItAll.Components.BaseLib;

public sealed class DialogService : IDisposable
{
    private readonly NavigationManager navigationManager;
    private readonly List<DialogReference> dialogs = [];
    private bool disposed;

    public DialogService(NavigationManager navigationManager)
    {
        this.navigationManager = navigationManager;
        this.navigationManager.LocationChanged += HandleLocationChanged;
    }

    public event Action? Changed;

    public IReadOnlyList<DialogReference> Dialogs => dialogs;

    public Task<object?> OpenAsync<TComponent>(
        string title,
        IReadOnlyDictionary<string, object?>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        return OpenAsync(title, typeof(TComponent), parameters, options, cancellationToken);
    }

    public Task<object?> OpenAsync(
        string title,
        Type componentType,
        IReadOnlyDictionary<string, object?>? parameters = null,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException("The dialog component type must implement IComponent.", nameof(componentType));
        }

        return OpenCore(
            title,
            componentType,
            parameters ?? new Dictionary<string, object?>(),
            childContent: null,
            options,
            cancellationToken);
    }

    public Task<object?> OpenAsync(
        string title,
        RenderFragment<DialogReference> childContent,
        DialogOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(childContent);

        return OpenCore(
            title,
            componentType: null,
            parameters: new Dictionary<string, object?>(),
            childContent,
            options,
            cancellationToken);
    }

    public Task CloseAsync(object? result = null)
    {
        var dialog = dialogs.LastOrDefault();
        return dialog is null
            ? Task.CompletedTask
            : CloseAsync(dialog, result);
    }

    public Task CloseAsync(DialogReference dialog, object? result = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (!dialogs.Remove(dialog))
        {
            return Task.CompletedTask;
        }

        dialog.TrySetResult(result);
        NotifyChanged();
        return Task.CompletedTask;
    }

    public void Close(object? result = null)
    {
        _ = CloseAsync(result);
    }

    public void Close(DialogReference dialog, object? result = null)
    {
        _ = CloseAsync(dialog, result);
    }

    public void CloseAll(object? result = null)
    {
        foreach (var dialog in dialogs.ToArray())
        {
            dialog.TrySetResult(result);
        }

        dialogs.Clear();
        NotifyChanged();
    }

    private Task<object?> OpenCore(
        string title,
        Type? componentType,
        IReadOnlyDictionary<string, object?> parameters,
        RenderFragment<DialogReference>? childContent,
        DialogOptions? options,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var resolvedOptions = options?.Clone() ?? new DialogOptions();
        resolvedOptions.Title = title;

        var reference = new DialogReference(
            this,
            title,
            componentType,
            parameters,
            childContent,
            resolvedOptions);

        if (cancellationToken.CanBeCanceled)
        {
            reference.RegisterCancellation(cancellationToken);
        }

        dialogs.Add(reference);
        NotifyChanged();
        return reference.Result;
    }

    internal Task CancelAsync(DialogReference dialog, CancellationToken cancellationToken)
    {
        if (!dialogs.Remove(dialog))
        {
            return Task.CompletedTask;
        }

        dialog.TrySetCanceled(cancellationToken);
        NotifyChanged();
        return Task.CompletedTask;
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        CloseAll();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        navigationManager.LocationChanged -= HandleLocationChanged;
        foreach (var dialog in dialogs.ToArray())
        {
            dialog.TrySetCanceled();
        }

        dialogs.Clear();
    }
}

public sealed class DialogReference
{
    private readonly DialogService owner;
    private readonly TaskCompletionSource<object?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration cancellationRegistration;

    internal DialogReference(
        DialogService owner,
        string title,
        Type? componentType,
        IReadOnlyDictionary<string, object?> parameters,
        RenderFragment<DialogReference>? childContent,
        DialogOptions options)
    {
        this.owner = owner;
        Id = Guid.NewGuid();
        Title = title;
        ComponentType = componentType;
        Parameters = parameters.ToDictionary(static pair => pair.Key, static pair => pair.Value!);
        ChildContent = childContent;
        Options = options;
    }

    public Guid Id { get; }

    public string Title { get; }

    public Type? ComponentType { get; }

    public IDictionary<string, object> Parameters { get; }

    public RenderFragment<DialogReference>? ChildContent { get; }

    public DialogOptions Options { get; }

    public Task<object?> Result => completion.Task;

    public Task CloseAsync(object? result = null)
    {
        return owner.CloseAsync(this, result);
    }

    internal void RegisterCancellation(CancellationToken cancellationToken)
    {
        cancellationRegistration = cancellationToken.Register(() => _ = owner.CancelAsync(this, cancellationToken));
    }

    internal void TrySetResult(object? result)
    {
        cancellationRegistration.Dispose();
        completion.TrySetResult(result);
    }

    internal void TrySetCanceled(CancellationToken cancellationToken = default)
    {
        cancellationRegistration.Dispose();
        if (cancellationToken.CanBeCanceled)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        else
        {
            completion.TrySetCanceled();
        }
    }
}

public sealed class DialogOptions
{
    public string? Title { get; internal set; }

    public string? Eyebrow { get; set; }

    public string? Subtitle { get; set; }

    public string? HelpText { get; set; }

    public string? HelpHeading { get; set; }

    public ModalSize Size { get; set; } = ModalSize.Medium;

    public string? AriaLabel { get; set; }

    public string? TestId { get; set; }

    public string? Class { get; set; }

    public string? Style { get; set; }

    public bool CloseOnBackdrop { get; set; } = true;

    public bool ShowCloseButton { get; set; } = true;

    public bool DenseChrome { get; set; }

    public object? ChromeCloseResult { get; set; }

    public RenderFragment? HeaderActions { get; set; }

    public RenderFragment<DialogReference>? FooterContent { get; set; }

    internal DialogOptions Clone()
    {
        return new DialogOptions
        {
            Title = Title,
            Eyebrow = Eyebrow,
            Subtitle = Subtitle,
            HelpText = HelpText,
            HelpHeading = HelpHeading,
            Size = Size,
            AriaLabel = AriaLabel,
            TestId = TestId,
            Class = Class,
            Style = Style,
            CloseOnBackdrop = CloseOnBackdrop,
            ShowCloseButton = ShowCloseButton,
            DenseChrome = DenseChrome,
            ChromeCloseResult = ChromeCloseResult,
            HeaderActions = HeaderActions,
            FooterContent = FooterContent
        };
    }
}
