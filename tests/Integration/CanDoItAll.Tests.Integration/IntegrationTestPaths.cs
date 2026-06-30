namespace CanDoItAll.Tests.Integration;

internal static class IntegrationTestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string BuildConfiguration { get; } = ResolveBuildConfiguration();

    public static string ResolveProjectOutputAssembly(string projectDirectoryName, string assemblyFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyFileName);

        var assemblyPath = Path.Combine(
            RepositoryRoot,
            "src",
            projectDirectoryName,
            "bin",
            BuildConfiguration,
            "net10.0",
            assemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Expected MCP server assembly was not built for configuration '{BuildConfiguration}'.",
                assemblyPath);
        }

        return assemblyPath;
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
