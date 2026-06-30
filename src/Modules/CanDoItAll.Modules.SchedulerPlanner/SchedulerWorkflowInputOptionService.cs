using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.SchedulerPlanner;

public sealed record SchedulerWorkflowInputOptionQuery(
    WorkflowInputParameterDescriptor Parameter,
    IReadOnlyDictionary<string, string> CurrentValues);

public interface ISchedulerWorkflowInputOptionProvider
{
    WorkflowInputParameterOptionSourceKind SourceKind { get; }

    Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        SchedulerWorkflowInputOptionQuery query,
        CancellationToken cancellationToken = default);
}

public interface ISchedulerWorkflowInputOptionService
{
    Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> currentValues,
        CancellationToken cancellationToken = default);
}

public sealed class SchedulerWorkflowInputOptionService(
    IEnumerable<ISchedulerWorkflowInputOptionProvider> providers) : ISchedulerWorkflowInputOptionService
{
    public async Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
        WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> currentValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(currentValues);

        if (parameter.OptionSource.Kind == WorkflowInputParameterOptionSourceKind.None)
        {
            return [];
        }

        if (parameter.OptionSource.Kind == WorkflowInputParameterOptionSourceKind.Static)
        {
            return SnapshotDistinct(parameter.OptionSource.StaticOptions);
        }

        var query = new SchedulerWorkflowInputOptionQuery(
            parameter,
            currentValues.ToDictionary(StringComparer.Ordinal));
        var resolved = new List<WorkflowInputParameterOption>();
        foreach (var provider in providers.Where(provider => provider.SourceKind == parameter.OptionSource.Kind))
        {
            var options = await provider.ListOptionsAsync(query, cancellationToken);
            resolved.AddRange(options);
        }

        return SnapshotDistinct(resolved);
    }

    private static IReadOnlyList<WorkflowInputParameterOption> SnapshotDistinct(
        IEnumerable<WorkflowInputParameterOption> options)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .Where(option => seen.Add(option.Value.Trim()))
            .Select(option => option with
            {
                Value = option.Value.Trim(),
                Label = string.IsNullOrWhiteSpace(option.Label) ? option.Value.Trim() : option.Label.Trim(),
                Description = option.Description.Trim()
            })
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
