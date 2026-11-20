namespace CanDoItAll.RemoteInstances;

public interface IRemoteInstanceEditing {
    IRemoteInstanceOpening Open(Guid editorId, Func<bool> owns);
}

public interface IRemoteInstanceOpening : IAsyncDisposable {
    Task StartAsync(Func<Task> changed, Func<Task> retired);
    Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken);
    Task<T> WriteAsync<T>(Func<Task<T>> write, Func<bool> current, CancellationToken cancellationToken);
    void Stop();
}
