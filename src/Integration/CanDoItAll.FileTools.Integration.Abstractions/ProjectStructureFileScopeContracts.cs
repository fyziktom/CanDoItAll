namespace CanDoItAll.FileTools.Integration;

public sealed record FileToolsKnownFileScope(
    FileToolsSemanticScope Scope,
    FileToolsKnownFileOccurrence Occurrence);

public interface IProjectStructureNodeFileScopeProvider
{
    ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default);

    ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default);
}
