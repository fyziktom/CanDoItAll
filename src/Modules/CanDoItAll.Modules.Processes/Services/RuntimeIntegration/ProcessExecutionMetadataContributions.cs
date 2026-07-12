using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessExecutionMetadataContributionContext(
    ProcessRuntimeStepAssignment Assignment);

internal interface IProcessExecutionMetadataContribution
{
    string ContributionKey { get; }

    int Order { get; }

    IReadOnlyDictionary<string, object> BuildMetadata(
        ProcessExecutionMetadataContributionContext context);
}

internal sealed class ProcessExecutionMetadataComposer(
    IEnumerable<IProcessExecutionMetadataContribution> contributions)
{
    public static ProcessExecutionMetadataComposer Empty { get; } = new([]);

    private readonly IReadOnlyList<IProcessExecutionMetadataContribution> contributions = CreateContributions(contributions);

    internal string Compose(ProcessRuntimeStepAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var context = new ProcessExecutionMetadataContributionContext(assignment);
        var additions = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var contribution in contributions)
        {
            foreach (var item in contribution.BuildMetadata(context))
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    throw new InvalidOperationException(
                        $"Process execution metadata contribution '{contribution.ContributionKey}' returned an empty metadata key.");
                }

                if (!additions.TryAdd(item.Key, item.Value))
                {
                    throw new InvalidOperationException(
                        $"Process execution metadata key '{item.Key}' is owned by more than one contribution.");
                }
            }
        }

        return ProcessExecutionMetadataBuilder.BuildProcessExecutionMetadata(assignment, additions);
    }

    private static IReadOnlyList<IProcessExecutionMetadataContribution> CreateContributions(
        IEnumerable<IProcessExecutionMetadataContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var ordered = contributions
            .OrderBy(contribution => contribution.Order)
            .ThenBy(contribution => contribution.ContributionKey, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(contribution => string.IsNullOrWhiteSpace(contribution.ContributionKey)))
        {
            throw new InvalidOperationException(
                "A process execution metadata contribution must declare a stable contribution key.");
        }

        var duplicate = ordered
            .GroupBy(contribution => contribution.ContributionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate process execution metadata contribution key '{duplicate.Key}' is registered.");
        }

        return ordered;
    }
}
