namespace CanDoItAll.Tests.Unit;

internal static class TestRepositoryPath
{
    public static string Resolve(string repositoryRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var segments = relativePath.Split(
            ['\\', '/'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Path.Combine([repositoryRoot, .. segments]);
    }
}
