using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessEditingSession(IEditingChangeObserver changes, IEditingUiAccess access,
    Func<EditingScope> readScope, ILogger<EditingObservation> logger) : IAsyncDisposable {
    private readonly List<EditingObservation> observations = [];
    private CancellationTokenSource lifetime = new();
    private EditingScope? scope;
    private Guid? projectId;
    private bool disposed;
    private long changeSequence;
    private long acknowledgedSequence;
    public Guid EditorId { get; } = Guid.NewGuid();
    public bool IsReady { get; private set; }
    public bool IsUnavailable { get; private set; }
    public bool UpdatesAvailable => ChangeSequence > Interlocked.Read(ref acknowledgedSequence);
    public long ChangeSequence => Interlocked.Read(ref changeSequence);
    public void Acknowledge(long sequence) => Interlocked.Exchange(ref acknowledgedSequence, sequence);
    public CancellationToken Lifetime => lifetime.Token;
    public string Actor => IsReady ? observations[0].Actor : throw new UnauthorizedAccessException("Open current Process editing access first.");

    public async Task EnsureAsync(Guid? requestedProjectId, Func<Task> changed, Func<Task> retired) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsUnavailable) {
            throw new UnauthorizedAccessException("Reopen the Process workspace with current access.");
        }
        var current = readScope();
        if (IsReady && scope == current && projectId == requestedProjectId) {
            await AuthorizeAsync(false, lifetime.Token);
            return;
        }
        await CloseObservationsAsync();
        lifetime.Dispose();
        lifetime = new();
        scope = current;
        projectId = requestedProjectId;
        Acknowledge(ChangeSequence);
        var resources = requestedProjectId is { } id ? new[] { ProcessAuthoringChanges.GlobalResourceId, id } : [ProcessAuthoringChanges.GlobalResourceId];
        try {
            foreach (var resource in resources) {
                var observation = new EditingObservation(changes, access, new(current, EditingOwner.ProcessAuthoring, resource), logger);
                observations.Add(observation);
                await observation.StartAsync(async batch => {
                    if (disposed || IsUnavailable || batch.Changes.Count > 0 && batch.Changes.All(change => change.OriginEditorId == EditorId)) {
                        return;
                    }
                    Interlocked.Increment(ref changeSequence);
                    await changed();
                }, async () => {
                    if (disposed || IsUnavailable) {
                        return;
                    }
                    Retire();
                    await retired();
                }, catalog: true);
            }
            IsReady = true;
            await AuthorizeAsync(false, lifetime.Token);
        } catch {
            IsUnavailable = true;
            await CloseObservationsAsync();
            throw;
        }
    }

    public void Retire() {
        if (disposed || IsUnavailable) {
            return;
        }
        IsUnavailable = true;
        IsReady = false;
        lifetime.Cancel();
        foreach (var observation in observations) {
            observation.Stop();
        }
    }

    public async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        if (disposed || !IsReady || IsUnavailable || scope != readScope()) {
            throw new UnauthorizedAccessException("The original Process editing scope is no longer current.");
        }
        foreach (var observation in observations) {
            await observation.AuthorizeAsync(write, cancellationToken);
        }
        if (disposed || IsUnavailable || scope != readScope()) {
            throw new UnauthorizedAccessException("The original Process editing scope changed during authorization.");
        }
    }

    public async ValueTask<ProcessAuthoringUiAuthority> CaptureAsync(bool write, Func<bool> owns,
        CancellationToken cancellationToken = default) {
        if (!owns()) {
            throw new UnauthorizedAccessException("The Process editor is no longer current.");
        }
        await AuthorizeAsync(write, cancellationToken);
        var originalLifetime = Lifetime;
        var originalScope = scope ?? throw new UnauthorizedAccessException("Current Process editing access is required.");
        return new(originalScope, projectId, EditorId, Actor, async (mutation, token) => {
            if (originalLifetime.IsCancellationRequested || originalLifetime != Lifetime || !owns()) {
                throw new UnauthorizedAccessException("The original Process editor is no longer current.");
            }
            await AuthorizeAsync(mutation, token);
            if (originalLifetime.IsCancellationRequested || originalLifetime != Lifetime || !owns()) {
                throw new UnauthorizedAccessException("The original Process editor changed during authorization.");
            }
        });
    }

    private async Task CloseObservationsAsync() {
        IsReady = false;
        lifetime.Cancel();
        foreach (var observation in observations) {
            observation.Stop();
        }
        foreach (var observation in observations) {
            await observation.DisposeAsync();
        }
        observations.Clear();
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        await CloseObservationsAsync();
        lifetime.Dispose();
    }
}

public sealed record ProcessAuthoringUiAuthority(EditingScope Scope, Guid? ProjectId, Guid EditorId, string CallerId,
    Func<bool, CancellationToken, ValueTask> Authorize);
