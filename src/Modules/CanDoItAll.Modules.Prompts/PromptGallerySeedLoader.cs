using System.Text.Json;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptGallerySeedLoader
{
    public const string CatalogSource = "prompt-library-pack";
    public const string EmbeddedRoot = "embedded://CanDoItAll.Modules.Prompts/SeedAssets/PromptLibrary";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Lazy<PromptGallerySeedPack> _pack = new(
        LoadCore,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public PromptGallerySeedPack Load() => _pack.Value;

    private static PromptGallerySeedPack LoadCore()
    {
        var manifest = Read<PromptGallerySeedManifest>("manifest.json");
        var groups = Read<List<PromptGalleryGroupSeed>>("group-catalog.json");
        var components = Read<List<PromptGalleryComponentSeed>>("prompt-component-library.json");
        var flows = Read<List<PromptGalleryFlowSeed>>("factory-prompt-flow-templates.seed.json");
        var blueprints = Read<List<PromptGalleryBlueprintSeed>>("factory-prompt-blueprints.seed.json");
        Validate(manifest, groups, components, flows, blueprints);

        var groupsByKey = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var orderByKey = groups
            .OrderBy(group => group.Order)
            .SelectMany(group => group.ComponentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((key, index) => new { Key = key, Index = index })
            .ToDictionary(item => item.Key, item => item.Index, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            component.OrderIndex = orderByKey.GetValueOrDefault(component.Key, 10_000 + index);
            component.GroupMetadata = groupsByKey[component.Group];
        }

        for (var index = 0; index < flows.Count; index++)
        {
            flows[index].OrderIndex = index;
        }

        for (var index = 0; index < blueprints.Count; index++)
        {
            blueprints[index].OrderIndex = index;
        }

        return new PromptGallerySeedPack(
            EmbeddedRoot,
            manifest,
            groups.OrderBy(group => group.Order).ToList(),
            components.OrderBy(component => component.OrderIndex).ThenBy(component => component.Name).ToList(),
            flows.OrderBy(flow => flow.OrderIndex).ToList(),
            blueprints.OrderBy(blueprint => blueprint.OrderIndex).ToList());
    }

    private static T Read<T>(string fileName) where T : class
    {
        using var stream = OpenResource(fileName);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Embedded Prompt Gallery seed '{fileName}' deserialized to null.");
    }

    private static Stream OpenResource(string fileName)
    {
        var resourceName = $"CanDoItAll.Modules.Prompts.SeedAssets.PromptLibrary.{fileName}";
        return typeof(PromptGallerySeedLoader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Prompt Gallery seed resource '{resourceName}' was not found.");
    }

    private static void Validate(
        PromptGallerySeedManifest manifest,
        IReadOnlyList<PromptGalleryGroupSeed> groups,
        IReadOnlyList<PromptGalleryComponentSeed> components,
        IReadOnlyList<PromptGalleryFlowSeed> flows,
        IReadOnlyList<PromptGalleryBlueprintSeed> blueprints)
    {
        if (manifest.Version != 1)
        {
            throw new InvalidDataException($"Unsupported Prompt Gallery seed manifest version '{manifest.Version}'.");
        }

        if (manifest.ComponentCount != components.Count ||
            manifest.FlowCount != flows.Count ||
            manifest.BlueprintCount != blueprints.Count)
        {
            throw new InvalidDataException(
                "Prompt Gallery seed manifest counts do not match the embedded catalog files.");
        }

        EnsureUnique(groups.Select(group => group.Key), "group key");
        EnsureUnique(components.Select(component => component.Key), "component source key");
        EnsureUnique(components.Select(component => component.Id), "component ID");
        EnsureUnique(flows.Select(flow => flow.Key), "flow key");
        EnsureUnique(blueprints.Select(blueprint => blueprint.Key), "blueprint key");

        var groupsByKey = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var componentsByKey = components.ToDictionary(component => component.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Key) || string.IsNullOrWhiteSpace(group.Name))
            {
                throw new InvalidDataException("Prompt Gallery seed groups require a key and name.");
            }

            if (group.ComponentCount != group.ComponentKeys.Count)
            {
                throw new InvalidDataException(
                    $"Prompt Gallery seed group '{group.Key}' declares {group.ComponentCount} components but lists {group.ComponentKeys.Count} keys.");
            }

            foreach (var componentKey in group.ComponentKeys)
            {
                if (!componentsByKey.ContainsKey(componentKey))
                {
                    throw new InvalidDataException(
                        $"Prompt Gallery seed group '{group.Key}' references missing component '{componentKey}'.");
                }
            }
        }

        foreach (var component in components)
        {
            if (component.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(component.Key) ||
                string.IsNullOrWhiteSpace(component.Name) ||
                string.IsNullOrWhiteSpace(component.Content) ||
                component.Content.Length > PromptGalleryLimits.MaximumContentLength)
            {
                throw new InvalidDataException(
                    $"Prompt Gallery seed component '{component.Key}' requires a stable ID, key, name, and content.");
            }

            if (!groupsByKey.TryGetValue(component.Group, out var group) ||
                !group.ComponentKeys.Contains(component.Key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Prompt Gallery seed component '{component.Key}' is not declared by group '{component.Group}'.");
            }

            if (component.Tags.Any(string.IsNullOrWhiteSpace) ||
                component.StackTags.Any(string.IsNullOrWhiteSpace) ||
                component.TemplateTokens.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    $"Prompt Gallery seed component '{component.Key}' contains blank metadata values.");
            }
        }
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string label) where T : notnull
    {
        var duplicates = values
            .GroupBy(value => value, EqualityComparer<T>.Default)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidDataException(
                $"Prompt Gallery seed contains duplicate {label} values: {string.Join(", ", duplicates)}.");
        }
    }
}

public sealed record PromptGallerySeedPack(
    string Root,
    PromptGallerySeedManifest Manifest,
    IReadOnlyList<PromptGalleryGroupSeed> Groups,
    IReadOnlyList<PromptGalleryComponentSeed> Components,
    IReadOnlyList<PromptGalleryFlowSeed> Flows,
    IReadOnlyList<PromptGalleryBlueprintSeed> Blueprints);

public sealed class PromptGallerySeedManifest
{
    public int Version { get; set; }

    public string GeneratedBy { get; set; } = string.Empty;

    public int ComponentCount { get; set; }

    public int FlowCount { get; set; }

    public int BlueprintCount { get; set; }

    public int SimulationCount { get; set; }

    public int RecommendedComponentCount { get; set; }

    public int ToolboxComponentCount { get; set; }
}

public sealed class PromptGalleryGroupSeed
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string UiMode { get; set; } = string.Empty;

    public int Order { get; set; }

    public int ComponentCount { get; set; }

    public List<string> ComponentNames { get; set; } = [];

    public List<string> ComponentKeys { get; set; } = [];
}

public sealed class PromptGalleryComponentSeed
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string BlockKind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsRecommendedByDefault { get; set; }

    public string PromptTypeRules { get; set; } = string.Empty;

    public string BlueprintRules { get; set; } = string.Empty;

    public string PhaseRules { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public List<string> StackTags { get; set; } = [];

    public bool ToolboxEligible { get; set; }

    public List<string> TemplateTokens { get; set; } = [];

    public int OrderIndex { get; set; }

    public PromptGalleryGroupSeed? GroupMetadata { get; set; }
}

public sealed class PromptGalleryFlowSeed
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string BlockIdsJson { get; set; } = "[]";

    public string PromptTypeRules { get; set; } = string.Empty;

    public List<string> BlockKeys { get; set; } = [];

    public List<PromptGalleryFlowAgentSeed> AgentSequence { get; set; } = [];

    public int OrderIndex { get; set; }
}

public sealed class PromptGalleryFlowAgentSeed
{
    public int Order { get; set; }

    public Guid RoleComponentId { get; set; }

    public string RoleComponentKey { get; set; } = string.Empty;

    public string BlueprintKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public List<string> BlockKeys { get; set; } = [];
}

public sealed class PromptGalleryBlueprintSeed
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PromptType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Guidance { get; set; } = string.Empty;

    public Guid? RecommendedFlowTemplateId { get; set; }

    public string RecommendedFlowKey { get; set; } = string.Empty;

    public List<string> RecommendedBlockKeys { get; set; } = [];

    public int OrderIndex { get; set; }
}
