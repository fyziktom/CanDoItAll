using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public interface IProcessLaunchVariableContributor
{
    void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables);
}

public sealed class ProcessLaunchVariablePreparationService(
    IEnumerable<IProcessLaunchVariableContributor> registeredContributors,
    ProcessTemplatePackLoader? templatePackLoader = null)
{
    private readonly IReadOnlyList<IProcessLaunchVariableContributor> contributors = registeredContributors.ToArray();

    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        var preparedContext = ResolveTemplateDriverActivations(context, templatePackLoader);
        foreach (var contributor in contributors)
        {
            contributor.Enrich(preparedContext, variables);
        }
    }

    private static ProcessLaunchPreparationContext ResolveTemplateDriverActivations(
        ProcessLaunchPreparationContext context,
        ProcessTemplatePackLoader? templatePackLoader)
    {
        if (context.DriverActivations.Count > 0 ||
            templatePackLoader is null ||
            string.IsNullOrWhiteSpace(context.DefinitionKey))
        {
            return context;
        }

        var normalizedDefinitionKey = context.DefinitionKey.Trim();
        var pack = templatePackLoader.Load();
        if (!pack.Definitions.Any(definition =>
                string.Equals(definition.Key, normalizedDefinitionKey, StringComparison.OrdinalIgnoreCase)))
        {
            return context;
        }

        var definition = templatePackLoader.LoadDefinition(normalizedDefinitionKey);
        var activations = definition.LaunchDriverActivations
            .Where(activation => !string.IsNullOrWhiteSpace(activation.DriverKey))
            .Select(activation => new ProcessLaunchDriverActivation(
                activation.DriverKey.Trim(),
                new Dictionary<string, string>(activation.Settings, StringComparer.OrdinalIgnoreCase))
            {
                InputArtifactBindings = activation.InputArtifactBindings
                    .Select(binding => new ProcessLaunchDriverArtifactBinding(
                        binding.BindingKey.Trim(),
                        binding.SourceStepKey.Trim(),
                        binding.ArtifactExpectationKey.Trim(),
                        binding.PayloadSchema.Trim()))
                    .ToArray()
            })
            .ToArray();
        return context with { DriverActivations = activations };
    }
}

public sealed record ProcessLaunchPreparationContext(
    string? DefinitionKey,
    bool IsSubprocess,
    ProcessLaunchSourceSnapshot Source)
{
    public IReadOnlyList<ProcessLaunchDriverActivation> DriverActivations { get; init; } = [];
}

public sealed record ProcessLaunchDriverActivation(
    string DriverKey,
    IReadOnlyDictionary<string, string> Settings)
{
    public IReadOnlyList<ProcessLaunchDriverArtifactBinding> InputArtifactBindings { get; init; } = [];

    public bool TryGetSetting(string key, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Settings.TryGetValue(key, out var candidate) &&
            !string.IsNullOrWhiteSpace(candidate))
        {
            value = candidate.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }
}

public sealed record ProcessLaunchDriverArtifactBinding(
    string BindingKey,
    string SourceStepKey,
    string ArtifactExpectationKey,
    string PayloadSchema);

public sealed record ProcessLaunchSourceSnapshot(
    Guid ProjectId,
    string ProjectName,
    ProcessLaunchSourceItem SelectedItem,
    IReadOnlyList<ProcessLaunchSourceItem> ContextItems,
    string ContextSummary);

public sealed record ProcessLaunchSourceItem(
    string Id,
    string Title,
    string Subtitle,
    string Notes,
    string Subtype,
    string ArtifactKind,
    IReadOnlyList<string> Badges,
    ProcessLaunchSourceItemKind Kind,
    bool IsIncludedInProcessContext);

public enum ProcessLaunchSourceItemKind
{
    Other,
    ImageAsset
}
