namespace CanDoItAll.Modules.Plugins;

public sealed record DockerWorkflowExecutorSettings
{
    public string Image { get; init; } = "qdrant/qdrant:latest";

    public string ContainerName { get; init; } = "candoitall-qdrant-proof";

    public bool PullIfMissing { get; init; } = true;

    public IReadOnlyList<string> PortMappings { get; init; } = [];

    public int Tail { get; init; } = 120;

    public string Since { get; init; } = string.Empty;

    public int MaxOutputCharacters { get; init; } = 12000;
}
