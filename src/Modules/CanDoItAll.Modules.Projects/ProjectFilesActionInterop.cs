using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFilesActionInterop(IJSRuntime runtime, Func<CancellationToken, ValueTask> requireOrigin) : IJSRuntime {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        await requireOrigin(cancellationToken);
        var result = await runtime.InvokeAsync<TValue>(identifier, cancellationToken, args);
        return result is IJSObjectReference module
            ? (TValue)(object)new ProjectFilesActionModule(module, requireOrigin)
            : result;
    }
}

internal sealed class ProjectFilesActionModule(IJSObjectReference module, Func<CancellationToken, ValueTask> requireOrigin) : IJSObjectReference {
    private Exception? primaryFailure;
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        try {
            await requireOrigin(cancellationToken);
            return await module.InvokeAsync<TValue>(identifier, cancellationToken, args);
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await module.DisposeAsync();
        } catch (Exception cleanup) when (primaryFailure is not null && cleanup is not JSDisconnectedException) {
            throw new AggregateException("Project file delivery and module cleanup both failed.", primaryFailure, cleanup);
        }
    }
}
