using System.Runtime.CompilerServices;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Integration;

internal sealed class PricingTestRelayStream(SharedProviderRelayUsage usage, Func<Task>? beforeCompletion, bool allowLateCompletion = false) : ISharedProviderRelayStream {
    private readonly TaskCompletionSource<SharedProviderRelayStreamCompletion> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public SharedProviderRelayResponseHeaders Headers => SharedProviderRelayResponseHeaders.Empty;
    public Task<SharedProviderRelayStreamCompletion> Completion => completion.Task;

    public async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new(null, """{"choices":[{"delta":{"content":"ok"}}]}""");
        if (beforeCompletion is not null) {
            await beforeCompletion();
        }
        completion.TrySetResult(new(usage));
        yield return new(null, "[DONE]");
    }

    internal void Complete() => completion.TrySetResult(new(usage));

    public ValueTask DisposeAsync() {
        if (!allowLateCompletion) {
            completion.TrySetCanceled();
        }
        return ValueTask.CompletedTask;
    }
}
