namespace CanDoItAll.Tests.Unit;

internal static class AppDbContextModelRegistryTestCollectionNames
{
    public const string Name = "AppDbContextModelRegistry";
}

[CollectionDefinition(
    AppDbContextModelRegistryTestCollectionNames.Name,
    DisableParallelization = true)]
public sealed class AppDbContextModelRegistryTestCollection;
