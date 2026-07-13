namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureKnownFileInteractionSlot(
    Func<Guid, string, CancellationToken, ValueTask<ProjectStructureKnownFileInteraction>> openInteraction)
    : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly HashSet<OpenOperation> operations = [];
    private OpenOperation? activeOperation;
    private ProjectStructureKnownFileInteraction? current;
    private bool disposed;

    public ProjectStructureKnownFileInteraction? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public async ValueTask<ProjectStructureKnownFileInteraction?> OpenAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var operation = new OpenOperation(
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        OpenOperation? replacedOperation;
        ProjectStructureKnownFileInteraction? replacedInteraction;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            replacedOperation = activeOperation;
            activeOperation = operation;
            operations.Add(operation);
            replacedInteraction = current;
            current = null;
        }

        replacedOperation?.Cancel();
        ProjectStructureKnownFileInteraction? opened = null;
        try
        {
            if (replacedInteraction is not null)
            {
                await replacedInteraction.DisposeAsync();
            }

            opened = await openInteraction(projectId, nodeId, operation.Token);
            ProjectStructureKnownFileInteraction? accepted = null;
            lock (gate)
            {
                if (!disposed &&
                    ReferenceEquals(activeOperation, operation) &&
                    !operation.IsCancellationRequested)
                {
                    current = opened;
                    accepted = opened;
                    opened = null;
                }
            }

            return accepted;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (opened is not null)
            {
                await opened.DisposeAsync();
            }

            lock (gate)
            {
                operations.Remove(operation);
                if (ReferenceEquals(activeOperation, operation))
                {
                    activeOperation = null;
                }
            }

            operation.Complete();
        }
    }

    public async ValueTask CloseAsync()
    {
        OpenOperation? operation;
        ProjectStructureKnownFileInteraction? interaction;
        lock (gate)
        {
            operation = activeOperation;
            activeOperation = null;
            interaction = current;
            current = null;
        }

        operation?.Cancel();
        if (interaction is not null)
        {
            await interaction.DisposeAsync();
        }

        if (operation is not null)
        {
            await operation.Completion.Task;
        }
    }

    public async ValueTask DisposeAsync()
    {
        OpenOperation[] pending;
        ProjectStructureKnownFileInteraction? interaction;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            pending = [.. operations];
            activeOperation = null;
            interaction = current;
            current = null;
        }

        foreach (OpenOperation operation in pending)
        {
            operation.Cancel();
        }

        if (interaction is not null)
        {
            await interaction.DisposeAsync();
        }

        await Task.WhenAll(pending.Select(operation => operation.Completion.Task));
    }

    private sealed class OpenOperation(CancellationTokenSource cancellation)
    {
        private int completed;

        public CancellationToken Token { get; } = cancellation.Token;

        public bool IsCancellationRequested => Token.IsCancellationRequested;

        public TaskCompletionSource Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void Cancel()
        {
            if (Volatile.Read(ref completed) != 0)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref completed) != 0)
            {
            }
        }

        public void Complete()
        {
            Volatile.Write(ref completed, 1);
            Completion.TrySetResult();
            cancellation.Dispose();
        }
    }
}
