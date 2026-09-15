namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class DatabaseCanonicalityArchitectureTests
{
    [Fact]
    public void Profile_specific_db_context_factory_is_limited_to_explicit_maintenance_boundaries()
    {
        var root = FindRepositoryRoot();
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(root, "src/Foundation/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs"),
            NormalizePath(root, "src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs"),
            NormalizePath(root, "src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferService.cs"),
            NormalizePath(root, "src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs"),
            NormalizePath(root, "src/Modules/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs"),
            NormalizePath(root, "src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferOperationRunner.cs")
        };

        var actualFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("IProfileAppDbContextFactory", StringComparison.Ordinal))
            .Select(path => NormalizePath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(allowedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase), actualFiles);
    }

    [Fact]
    public void Product_modules_do_not_reach_the_global_app_db_context()
    {
        // The pooled AppDbContext factory exists for migrations and bounded maintenance composition only. Module
        // services own their persistence through their own contexts and contracts; a module that injects the global
        // context or its factory would reacquire every foreign entity as a back door.
        var root = FindRepositoryRoot();
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(root, "src/Modules/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs")
        };
        string[] forbiddenTokens =
        [
            "IDbContextFactory<AppDbContext>",
            "AppDbContext dbContext",
            "AppDbContext context",
            "GetRequiredService<AppDbContext>",
            "GetService<AppDbContext>"
        ];

        var actualFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Modules"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
            {
                var content = File.ReadAllText(path);
                return forbiddenTokens.Any(token => content.Contains(token, StringComparison.Ordinal));
            })
            .Select(path => NormalizePath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(allowedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase), actualFiles);
    }

    private static bool IsBuildOutput(string path)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        return path.Split(separators).Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
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

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    private static string NormalizePath(string root, string relativePath)
    {
        return NormalizePath(Path.Combine(root, relativePath));
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }
}
