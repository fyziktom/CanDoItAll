using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileQueryServiceTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFileQueryServiceTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> externalRoots = [];

    [Fact]
    public void ListFiles_supports_recursive_globstar_all_pattern()
    {
        var appRoot = CreateDirectory("apps", "TrailReport");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        WriteFile(appRoot, "Features", "ReportService.cs", "public sealed class ReportService {}");
        WriteFile(appRoot, "obj", "Debug", "Generated.cs", "public sealed class Generated {}");
        var service = CreateService();

        var result = service.ListFiles("apps/TrailReport", "**/*", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("**/*", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/TrailReport/Program.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/TrailReport/Features/ReportService.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_supports_recursive_globstar_extension_pattern()
    {
        var appRoot = CreateDirectory("apps", "MenuPlanner");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        WriteFile(appRoot, "Components", "Pages", "Home.razor", "<h1>Home</h1>");
        WriteFile(appRoot, "Components", "Layout", "MainLayout.razor", "<main>@Body</main>");
        WriteFile(appRoot, "README.md", "# Menu Planner");
        var service = CreateService();

        var result = service.ListFiles("apps/MenuPlanner", "**/*.razor", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("**/*.razor", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/MenuPlanner/Components/Pages/Home.razor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/MenuPlanner/Components/Layout/MainLayout.razor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_supports_external_target_globstar_pattern()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalRoot = CreateExternalDirectory("RecipeCards");
        WriteFile(externalRoot, "RecipeCards.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        WriteFile(externalRoot, "Components", "Pages", "Index.razor", "<h1>Recipes</h1>");
        var service = CreateService();
        var alias = BuildExternalTargetAlias(externalRoot);

        var result = service.ListFiles(alias, "**/*", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, $"{alias}/RecipeCards.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, $"{alias}/Components/Pages/Index.razor", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }

            foreach (var externalRoot in externalRoots)
            {
                if (Directory.Exists(externalRoot))
                {
                    Directory.Delete(externalRoot, recursive: true);
                }
            }
        }
        catch
        {
        }
    }

    private WorkspaceFileQueryService CreateService()
    {
        Directory.CreateDirectory(workspaceRoot);
        var pathPolicy = new WorkspacePathPolicy(workspaceRoot);
        return new WorkspaceFileQueryService(
            pathPolicy,
            new WorkspaceFileReceiptWriter(workspaceRoot),
            new WorkspaceTextContentGuard());
    }

    private string CreateDirectory(params string[] segments)
    {
        var path = Path.Combine(new[] { workspaceRoot }.Concat(segments).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateExternalDirectory(params string[] segments)
    {
        var root = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFileQueryServiceExternal", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(new[] { root }.Concat(segments).ToArray());
        Directory.CreateDirectory(path);
        externalRoots.Add(root);
        return path;
    }

    private static void WriteFile(string rootPath, params string[] segmentsAndContent)
    {
        var content = segmentsAndContent[^1];
        var path = Path.Combine(new[] { rootPath }.Concat(segmentsAndContent[..^1]).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string BuildExternalTargetAlias(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var root = Path.GetPathRoot(normalizedFullPath)
            ?? throw new InvalidOperationException($"Could not resolve a drive root for '{fullPath}'.");
        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 || trimmedRoot[1] != ':')
        {
            throw new InvalidOperationException($"External-target alias tests require a drive-letter path. Received '{fullPath}'.");
        }

        var driveLetter = char.ToUpperInvariant(trimmedRoot[0]);
        var relativeWithinDrive = normalizedFullPath.Length <= root.Length
            ? string.Empty
            : normalizedFullPath[root.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(relativeWithinDrive)
            ? $"external-target/{driveLetter}"
            : WorkspacePathPolicy.NormalizeRelativePath(Path.Combine("external-target", driveLetter.ToString(), relativeWithinDrive));
    }
}
