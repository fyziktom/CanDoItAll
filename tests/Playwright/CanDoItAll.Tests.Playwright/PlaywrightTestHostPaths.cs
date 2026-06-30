namespace CanDoItAll.Tests.Playwright;

internal static class PlaywrightTestHostPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string BuildConfiguration { get; } = ResolveBuildConfiguration();

    public static string BuildDotnetRunArguments(string projectPath, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return $"run --configuration {BuildConfiguration} --no-build --no-launch-profile --project {projectPath} --urls {baseUrl}";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the CanDoItAll repository root from the test output directory.");
    }

    private static string ResolveBuildConfiguration()
    {
        var configured = Environment.GetEnvironmentVariable("CANDOITALL_TEST_CONFIGURATION");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationDirectory = baseDirectory.Parent;
        if (!string.IsNullOrWhiteSpace(configurationDirectory?.Name))
        {
            return configurationDirectory.Name;
        }

        return "Debug";
    }
}
