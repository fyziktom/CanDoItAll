namespace CanDoItAll.Tests.Unit;

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
            NormalizePath(root, "src/Modules/CanDoItAll.Modules.Workbench/DatabaseTransfer/ProjectPackageService.cs")
        };

        var unexpectedFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("IProfileAppDbContextFactory", StringComparison.Ordinal))
            .Select(path => NormalizePath(path))
            .Where(path => !allowedFiles.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Empty(unexpectedFiles);
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
