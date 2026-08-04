using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafProviderStreamingDispatchGate
{
    ValueTask<IAsyncDisposable> EnterAsync(
        ProviderProfile provider,
        string model,
        CancellationToken cancellationToken = default);
}

internal sealed class NoOpMafProviderStreamingDispatchGate : IMafProviderStreamingDispatchGate
{
    public static NoOpMafProviderStreamingDispatchGate Instance { get; } = new();

    public ValueTask<IAsyncDisposable> EnterAsync(
        ProviderProfile provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IAsyncDisposable>(NoOpAsyncDisposable.Instance);
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static NoOpAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class MafProviderStreamingDispatchGate(IProviderDispatchLaneGate dispatchLaneGate)
    : IMafProviderStreamingDispatchGate
{
    public ValueTask<IAsyncDisposable> EnterAsync(
        ProviderProfile provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var query = new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            model);
        return dispatchLaneGate.EnterAsync(
            query,
            AgentProviderCapabilityKind.ChatCompletion.ToString(),
            cancellationToken);
    }
}
