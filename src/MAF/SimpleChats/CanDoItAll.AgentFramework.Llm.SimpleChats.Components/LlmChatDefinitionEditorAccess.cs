using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public interface ILlmChatDefinitionEditorAccess {
    Task<LlmChatEditorOpening> OpenAsync(Guid? target, Guid editorId, Func<Task> changed,
        Func<Task> retired, CancellationToken cancellationToken);
    Task<LlmChatEditorOpening> OpenCatalogAsync(Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken);
}

public sealed class LlmChatEditorOpening(Guid? target, Guid editorId,
    Func<bool, CancellationToken, ValueTask> authorize, CancellationToken lifetime, Action stop, Func<ValueTask> dispose,
    LlmChatWriteResource resource = LlmChatWriteResource.Definition) : IAsyncDisposable {
    private Guid? boundTarget = target;
    private bool stopped;
    public Guid? Target => boundTarget;
    public CancellationToken Lifetime => lifetime;
    public bool IsCurrent => !stopped && !lifetime.IsCancellationRequested;

    public async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        if (stopped || lifetime.IsCancellationRequested) {
            throw new UnauthorizedAccessException("The original Simple Chat editing access was retired.");
        }
        await authorize(write, cancellationToken);
        if (stopped || lifetime.IsCancellationRequested) {
            throw new UnauthorizedAccessException("The original Simple Chat editing access changed.");
        }
    }

    public void BindAccepted(Guid id) {
        if (id == Guid.Empty || boundTarget is { } current && current != id) {
            throw new InvalidOperationException("The accepted Simple Chat resource belongs to a different editor target.");
        }
        boundTarget = id;
    }

    public IDisposable EnterWrite(Func<CancellationToken, ValueTask>? validateOrigin = null) => LlmChatWriteAdmission.Enter(boundTarget,
        async token => {
            if (validateOrigin is not null) {
                await validateOrigin(token);
            }
            await AuthorizeAsync(true, token);
            if (validateOrigin is not null) {
                await validateOrigin(token);
            }
        }, editorId, resource);

    public void Stop() {
        stopped = true;
        stop();
    }

    public async ValueTask DisposeAsync() {
        Stop();
        await dispose();
    }
}

public sealed class UnavailableLlmChatDefinitionEditorAccess : ILlmChatDefinitionEditorAccess {
    public Task<LlmChatEditorOpening> OpenAsync(Guid? target, Guid editorId, Func<Task> changed,
        Func<Task> retired, CancellationToken cancellationToken)
        => Task.FromException<LlmChatEditorOpening>(new UnauthorizedAccessException("Original Simple Chat editing access is unavailable in this host."));
    public Task<LlmChatEditorOpening> OpenCatalogAsync(Func<Task> changed, Func<Task> retired, CancellationToken cancellationToken)
        => Task.FromException<LlmChatEditorOpening>(new UnauthorizedAccessException("Original Simple Chat catalog access is unavailable in this host."));
}
