namespace CanDoItAll.SharedKernel;

public sealed record AutomationSignalItem(
    string Area,
    string Title,
    string Description,
    string Route,
    string Tone,
    DateTimeOffset? DueAtUtc = null,
    int Count = 0);

public interface IAutomationSignalSource
{
    Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default);
}

public interface IAutomationSignalProvider
{
    Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default);
}

public sealed class CompositeAutomationSignalProvider(
    IEnumerable<IAutomationSignalSource> sources) : IAutomationSignalProvider
{
    public async Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default)
    {
        var batches = await Task.WhenAll(sources.Select(source => source.ListSignalsAsync(cancellationToken)));

        return batches
            .SelectMany(batch => batch)
            .OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Area, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToList();
    }
}
