namespace CanDoItAll.Tests.Unit;

public sealed class ManagerProcessArchitectureTests
{
    [Fact]
    public void Manager_process_creation_is_owned_by_the_B01_host()
    {
        var managerRoot = ResolveManagerRoot();
        var source = ReadManagerSources();

        Assert.DoesNotContain("Process.Start(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Process {", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProcessesByName", source, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceLongRunningProcessHost", File.ReadAllText(Path.Combine(managerRoot, "ManagerProcessOwnership.cs")), StringComparison.Ordinal);
        Assert.Contains("WorkspaceProcessTerminationMode.GracefulThenForceTree", File.ReadAllText(Path.Combine(managerRoot, "ManagerProcessOwnership.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void System_management_is_restricted_to_the_windows_leaf_adapter()
    {
        var managerRoot = ResolveManagerRoot();
        var offenders = Directory.EnumerateFiles(managerRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains("System.Management", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["WindowsManagerProcessDiscovery.cs"], offenders);
    }

    private static string ReadManagerSources()
        => string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(ResolveManagerRoot(), "*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

    private static string ResolveManagerRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tools", "App", "CanDoItAll.Manager");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Manager source directory.");
    }
}
