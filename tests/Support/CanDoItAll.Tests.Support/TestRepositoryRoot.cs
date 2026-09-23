namespace CanDoItAll.Tests.Support;

/// <summary>
/// Locates the CanDoItAll checkout that a test run belongs to. An explicit root from
/// <see cref="EnvironmentVariableName"/> must be a complete checkout and is never silently replaced by a directory
/// walk; otherwise the nearest ancestor of the test output that is a complete checkout wins, so an isolated output
/// root or a nested worktree resolves to its own sources rather than to an unrelated ancestor.
/// </summary>
public static class TestRepositoryRoot
{
    public const string EnvironmentVariableName = "CANDOITALL_TEST_REPOSITORY_ROOT";

    private static readonly string[] RequiredMarkers =
    [
        "CanDoItAll.slnx",
        Path.Combine("src", "App", "CanDoItAll.Web", "CanDoItAll.Web.csproj")
    ];

    private static readonly Lazy<string> Current = new(() => Resolve(
        Environment.GetEnvironmentVariable(EnvironmentVariableName),
        AppContext.BaseDirectory));

    /// <summary>The repository root for the current test process.</summary>
    public static string Find() => Current.Value;

    public static string Resolve(string? configuredRoot, string startDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            string explicitRoot = Normalize(configuredRoot.Trim());
            if (!IsRepositoryRoot(explicitRoot))
            {
                throw new InvalidOperationException(
                    $"{EnvironmentVariableName} does not identify a complete CanDoItAll checkout " +
                    "(CanDoItAll.slnx and src/App/CanDoItAll.Web/CanDoItAll.Web.csproj are required).");
            }

            return explicitRoot;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        for (var directory = new DirectoryInfo(Normalize(startDirectory)); directory is not null; directory = directory.Parent)
        {
            if (IsRepositoryRoot(directory.FullName))
            {
                return Normalize(directory.FullName);
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate a CanDoItAll checkout above the test output directory. Set {EnvironmentVariableName} " +
            "for a non-standard output layout.");
    }

    public static bool IsRepositoryRoot(string directory)
        => !string.IsNullOrWhiteSpace(directory) &&
           RequiredMarkers.All(marker => File.Exists(Path.Combine(directory, marker)));

    private static string Normalize(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
