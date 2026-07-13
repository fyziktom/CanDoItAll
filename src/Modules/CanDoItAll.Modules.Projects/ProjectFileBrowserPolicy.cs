using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectFileBrowserPolicy
{
    public const int MaximumProjectSources = 64;

    private static readonly FileBrowserSearchBudget SearchBudget = new(
        maximumContainers: 32,
        maximumItems: 2_000,
        maximumDuration: TimeSpan.FromSeconds(5),
        maximumConcurrentRequests: 1,
        maximumMatches: 200,
        maximumRetainedBytes: 2L * 1024 * 1024);

    public static FileBrowserSession Create(
        IEnumerable<IFileBrowserProvider> providers,
        FileBrowserSortDescriptor defaultSort)
        => new(providers, CreateOptions(defaultSort));

    public static FileBrowserSession Create(
        FileBrowserSourceSet sources,
        FileBrowserSortDescriptor defaultSort)
        => new(sources, options: CreateOptions(defaultSort));

    public static bool IsSupportedTextFile(string fileName, string? mediaType)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static FileBrowserSessionOptions CreateOptions(FileBrowserSortDescriptor defaultSort)
        => new(
            pageSize: 50,
            defaultSort: defaultSort,
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: SearchBudget);
}
