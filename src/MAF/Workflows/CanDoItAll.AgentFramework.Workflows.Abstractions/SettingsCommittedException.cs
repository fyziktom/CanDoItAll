namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed class SettingsCommittedException<T>(T accepted, Exception innerException)
    : Exception("Settings were saved, but change notification needs a refresh. Do not replay the save.", innerException) where T : class {
    public T Accepted { get; } = accepted;
}
