using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents.Navigation;

public enum UrlHistoryMode {
    Push,
    Replace
}

public interface IAppNavigationHistory {
    ValueTask BackAsync(CancellationToken cancellationToken = default);

    // The state the host keeps for the current entry, or null when it cannot tell. Blazor starts a circuit without the
    // HistoryEntryState of the entry it starts on, although the browser keeps it across a reload.
    ValueTask<string?> GetEntryStateAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);
}

public sealed class BrowserNavigationHistory(IJSRuntime js) : IAppNavigationHistory {
    public ValueTask BackAsync(CancellationToken cancellationToken = default)
        => js.InvokeVoidAsync("history.back", cancellationToken);

    // Blazor keeps HistoryEntryState as userState in the browser entry. A runtime that cannot answer (prerendering, a
    // disconnected circuit, one without property reads, an unexpected state shape) leaves the state unknown, and the
    // binding falls back to Replace.
    public async ValueTask<string?> GetEntryStateAsync(CancellationToken cancellationToken = default) {
        try {
            var state = await js.GetValueAsync<BrowserEntryState?>("history.state", cancellationToken);
            return state?.UserState;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception) {
            return null;
        }
    }

    private sealed record BrowserEntryState(string? UserState);
}

public static class AppNavigationServiceCollectionExtensions {
    public static IServiceCollection AddAppNavigation(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IAppNavigationHistory, BrowserNavigationHistory>();
        return services;
    }
}
