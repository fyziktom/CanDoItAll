using CanDoItAll.AppComponents.Navigation;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit.Navigation;

public sealed class BrowserNavigationHistoryTests {
    [Fact]
    public async Task A_runtime_that_cannot_read_the_entry_reports_no_state_instead_of_failing() {
        var history = new BrowserNavigationHistory(new InvokeOnlyJSRuntime());

        Assert.Null(await history.GetEntryStateAsync());
    }

    [Fact]
    public async Task A_requested_cancellation_still_cancels() {
        var history = new BrowserNavigationHistory(new CancellingJSRuntime());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await history.GetEntryStateAsync(new CancellationToken(canceled: true)));
    }

    // Implements only the members every runtime must have, like test runtimes that predate .NET 10 property reads.
    private class InvokeOnlyJSRuntime : IJSRuntime {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }

    private sealed class CancellingJSRuntime : InvokeOnlyJSRuntime, IJSRuntime {
        public ValueTask<TValue> GetValueAsync<TValue>(string identifier, CancellationToken cancellationToken)
            => ValueTask.FromCanceled<TValue>(cancellationToken);
    }
}
