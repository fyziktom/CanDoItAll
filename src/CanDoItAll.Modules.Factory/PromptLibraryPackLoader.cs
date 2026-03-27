using System.Reflection;
using System.Text.Json;

namespace CanDoItAll.Modules.Factory;

public sealed class PromptLibraryPackLoader
{
    public const string CatalogSource = "prompt-library-pack";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Lazy<PromptLibraryPack> _pack;

    public PromptLibraryPackLoader()
    {
        _pack = new Lazy<PromptLibraryPack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public PromptLibraryPack Load() => _pack.Value;

    private static PromptLibraryPack LoadCore()
    {
        var packRoot = ResolvePackRoot();
        var groups = ReadJson<List<PromptLibraryGroupSeed>>(Path.Combine(packRoot, "group-catalog.json"));
        var components = ReadJson<List<PromptBlockSeed>>(Path.Combine(packRoot, "prompt-component-library.json"));
        var flows = ReadJson<List<PromptFlowTemplateSeed>>(Path.Combine(packRoot, "factory-prompt-flow-templates.seed.json"));
        var blueprints = ReadJson<List<PromptBlueprintSeed>>(Path.Combine(packRoot, "factory-prompt-blueprints.seed.json"));

        var groupsByKey = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var componentOrderLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var componentOrder = 0;
        foreach (var group in groups.OrderBy(item => item.Order))
        {
            if (group.ComponentKeys is null)
            {
                continue;
            }

            foreach (var componentKey in group.ComponentKeys)
            {
                if (!componentOrderLookup.ContainsKey(componentKey))
                {
                    componentOrderLookup[componentKey] = componentOrder++;
                }
            }
        }

        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            component.OrderIndex = componentOrderLookup.TryGetValue(component.Key, out var orderedIndex)
                ? orderedIndex
                : 10_000 + index;

            if (groupsByKey.TryGetValue(component.Group, out var group))
            {
                component.GroupOrder = group.Order;
                component.GroupName = group.Name;
                component.GroupUiMode = group.UiMode;
            }
        }

        for (var index = 0; index < flows.Count; index++)
        {
            flows[index].OrderIndex = index;
        }

        for (var index = 0; index < blueprints.Count; index++)
        {
            blueprints[index].OrderIndex = index;
        }

        return new PromptLibraryPack(
            packRoot,
            groups.OrderBy(item => item.Order).ToList(),
            components.OrderBy(item => item.OrderIndex).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            flows.OrderBy(item => item.OrderIndex).ToList(),
            blueprints.OrderBy(item => item.OrderIndex).ToList());
    }

    private static T ReadJson<T>(string path) where T : class, new()
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
    }

    private static string ResolvePackRoot()
    {
        var candidateStarts = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var start in candidateStarts)
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "output", "prompt-library", "manifest.json");
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate)!;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("Unable to locate output/prompt-library/manifest.json from the current application base path.");
    }
}

public sealed record PromptLibraryPack(
    string RootPath,
    IReadOnlyList<PromptLibraryGroupSeed> Groups,
    IReadOnlyList<PromptBlockSeed> Components,
    IReadOnlyList<PromptFlowTemplateSeed> Flows,
    IReadOnlyList<PromptBlueprintSeed> Blueprints);

public sealed class PromptLibraryGroupSeed
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

public sealed class PromptBlockSeed
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

    public int GroupOrder { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string GroupUiMode { get; set; } = string.Empty;
}

public sealed class PromptFlowTemplateSeed
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string BlockIdsJson { get; set; } = "[]";

    public string PromptTypeRules { get; set; } = string.Empty;

    public List<string> BlockKeys { get; set; } = [];

    public List<PromptFlowAgentSeed> AgentSequence { get; set; } = [];

    public int OrderIndex { get; set; }
}

public sealed class PromptFlowAgentSeed
{
    public int Order { get; set; }

    public Guid RoleComponentId { get; set; }

    public string RoleComponentKey { get; set; } = string.Empty;

    public string BlueprintKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public List<string> BlockKeys { get; set; } = [];
}

public sealed class PromptBlueprintSeed
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


