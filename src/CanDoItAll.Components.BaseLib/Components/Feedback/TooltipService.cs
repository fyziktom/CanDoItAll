using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Components.BaseLib;

public sealed class TooltipService : IDisposable
{
    private readonly NavigationManager navigationManager;
    private CancellationTokenSource? activeLifetime;
    private bool disposed;

    public TooltipService(NavigationManager navigationManager)
    {
        this.navigationManager = navigationManager;
        this.navigationManager.LocationChanged += HandleLocationChanged;
    }

    public event Action? Changed;

    public TooltipState? Current { get; private set; }

    public void Open(MouseEventArgs args, string text, TooltipOptions? options = null)
    {
        Open(text, args.ClientX, args.ClientY, options);
    }

    public void Open(string text, double clientX, double clientY, TooltipOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        OpenCore(text, childContent: null, clientX, clientY, options);
    }

    public void Open(
        RenderFragment<TooltipService> childContent,
        double clientX,
        double clientY,
        TooltipOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(childContent);
        OpenCore(text: null, childContent, clientX, clientY, options);
    }

    public void Close()
    {
        activeLifetime?.Cancel();
        activeLifetime?.Dispose();
        activeLifetime = null;

        if (Current is null)
        {
            return;
        }

        Current = null;
        NotifyChanged();
    }

    private void OpenCore(
        string? text,
        RenderFragment<TooltipService>? childContent,
        double clientX,
        double clientY,
        TooltipOptions? options)
    {
        ThrowIfDisposed();

        var resolvedOptions = options?.Clone() ?? new TooltipOptions();
        var state = new TooltipState(
            Guid.NewGuid(),
            text,
            childContent,
            clientX,
            clientY,
            resolvedOptions);

        activeLifetime?.Cancel();
        activeLifetime?.Dispose();
        activeLifetime = new CancellationTokenSource();
        _ = ShowWithLifetimeAsync(state, activeLifetime.Token);
    }

    private async Task ShowWithLifetimeAsync(TooltipState state, CancellationToken cancellationToken)
    {
        try
        {
            if (state.Options.Delay is { } delay && delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            Current = state;
            NotifyChanged();

            if (state.Options.Duration is { } duration && duration > TimeSpan.Zero)
            {
                await Task.Delay(duration, cancellationToken);
                if (Current?.Id == state.Id)
                {
                    Current = null;
                    NotifyChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        Close();
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
        activeLifetime?.Cancel();
        activeLifetime?.Dispose();
    }
}

public sealed record TooltipState(
    Guid Id,
    string? Text,
    RenderFragment<TooltipService>? ChildContent,
    double ClientX,
    double ClientY,
    TooltipOptions Options);

public sealed class TooltipOptions
{
    public TooltipPosition Position { get; set; } = TooltipPosition.Top;

    public TimeSpan? Delay { get; set; }

    public TimeSpan? Duration { get; set; } = TimeSpan.FromSeconds(4);

    public string? Class { get; set; }

    public string? Style { get; set; }

    public string? TestId { get; set; }

    public bool CloseOnMouseLeave { get; set; } = true;

    internal TooltipOptions Clone()
    {
        return new TooltipOptions
        {
            Position = Position,
            Delay = Delay,
            Duration = Duration,
            Class = Class,
            Style = Style,
            TestId = TestId,
            CloseOnMouseLeave = CloseOnMouseLeave
        };
    }
}

public enum TooltipPosition
{
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    LeftTop,
    LeftBottom,
    RightTop,
    RightBottom
}
