namespace CanDoItAll.Modules.Plugins;

public sealed class PluginCommittedException<T>(T value, Exception innerException)
    : Exception("The plugin operation committed before a follow-up step failed.", innerException) {
    public T Value { get; } = value;
}

public sealed class PluginPackageStageException(PluginPackageProgress progress, Exception innerException)
    : Exception("A plugin package stage completed before a later step failed.", innerException) {
    public PluginPackageProgress Progress { get; } = progress;
}

internal static class PluginCommit {
    public static void Observe<T>(T value, Action action) {
        try {
            action();
        } catch (Exception exception) {
            throw new PluginCommittedException<T>(value, exception);
        }
    }

    public static async Task ObserveAsync<T>(T value, Func<Task> action) {
        try {
            await action();
        } catch (Exception exception) {
            throw new PluginCommittedException<T>(value, exception);
        }
    }
}
