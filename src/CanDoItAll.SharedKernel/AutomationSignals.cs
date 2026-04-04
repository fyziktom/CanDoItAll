namespace CanDoItAll.SharedKernel;

public sealed record AutomationSignalItem(
    string Area,
    string Title,
    string Description,
    string Route,
    string Tone,
    DateTimeOffset? DueAtUtc = null,
    int Count = 0);

public interface IAutomationSignalProvider
{
    Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default);
}

public sealed class NullAutomationSignalProvider : IAutomationSignalProvider
{
    public Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AutomationSignalItem>>([]);
}
