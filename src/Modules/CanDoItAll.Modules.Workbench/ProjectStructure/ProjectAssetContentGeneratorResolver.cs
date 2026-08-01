namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAssetContentGeneratorResolver
{
    private readonly IReadOnlyDictionary<ProjectAssetGenerationKind, IProjectAssetContentGenerator> generators;

    public ProjectAssetContentGeneratorResolver(IEnumerable<IProjectAssetContentGenerator> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);

        var resolved = new Dictionary<ProjectAssetGenerationKind, IProjectAssetContentGenerator>();
        foreach (IProjectAssetContentGenerator? generator in generators)
        {
            if (generator is null)
            {
                throw new ArgumentException("Asset content generators cannot contain null entries.", nameof(generators));
            }

            if (!resolved.TryAdd(generator.GenerationKind, generator))
            {
                throw new InvalidOperationException(
                    $"Multiple asset content generators are registered for '{generator.GenerationKind}'.");
            }
        }

        this.generators = resolved;
    }

    public IProjectAssetContentGenerator Resolve(ProjectAssetGenerationKind generationKind)
    {
        if (generators.TryGetValue(generationKind, out var generator))
        {
            return generator;
        }

        throw new ProjectAssetCreationException(
            ProjectAssetCreationErrorCode.UnsupportedGenerationKind,
            $"Asset content generation kind '{generationKind}' is not supported.");
    }
}
