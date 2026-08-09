using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureDotNetProjectTargetResolverTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"CanDoItAll.ProjectTargetResolver.{Guid.NewGuid():N}");

    public ProjectStructureDotNetProjectTargetResolverTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void Resolve_accepts_an_exact_existing_project_file()
    {
        var projectPath = CreateProject("Calculator", "Calculator.csproj");
        var sut = CreateSut();

        var result = sut.Resolve(projectPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(projectPath, result.ProjectFilePath);
    }

    [Fact]
    public void Resolve_resolves_a_directory_with_one_top_level_project()
    {
        var projectPath = CreateProject("Calculator", "Calculator.csproj");
        var sut = CreateSut();

        var result = sut.Resolve(Path.GetDirectoryName(projectPath)!);

        Assert.True(result.IsSuccess);
        Assert.Equal(projectPath, result.ProjectFilePath);
    }

    [Fact]
    public void Resolve_rejects_a_solution_root_with_only_nested_projects()
    {
        _ = CreateProject("Calculator", "Calculator.csproj");
        _ = CreateProject("Calculator.Tests", "Calculator.Tests.csproj");
        File.WriteAllText(Path.Combine(root, "Calculator.slnx"), "<Solution />");
        var sut = CreateSut();

        var result = sut.Resolve(root);

        Assert.False(result.IsSuccess);
        Assert.Contains("no top-level", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact application project file", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_rejects_a_directory_with_multiple_top_level_projects()
    {
        _ = CreateProject(string.Empty, "Calculator.csproj");
        _ = CreateProject(string.Empty, "Calculator.Tests.csproj");
        var sut = CreateSut();

        var result = sut.Resolve(root);

        Assert.False(result.IsSuccess);
        Assert.Contains("multiple top-level", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact application project file", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Calculator.sln")]
    [InlineData("Calculator.slnx")]
    public void Resolve_rejects_solution_files(string fileName)
    {
        var solutionPath = Path.Combine(root, fileName);
        File.WriteAllText(solutionPath, string.Empty);
        var sut = CreateSut();

        var result = sut.Resolve(solutionPath);

        Assert.False(result.IsSuccess);
        Assert.Contains("solution files are not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_rejects_an_unverified_project_file()
    {
        var sut = CreateSut();

        var result = sut.Resolve(Path.Combine(root, "Missing", "Missing.csproj"));

        Assert.False(result.IsSuccess);
        Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not save", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_rejects_a_project_directory_reached_through_a_symbolic_link()
    {
        var projectPath = CreateProject("real", "Calculator.csproj");
        var linkedDirectory = Path.Combine(root, "linked");
        Directory.CreateSymbolicLink(linkedDirectory, Path.GetDirectoryName(projectPath)!);
        var sut = CreateSut();

        try
        {
            var result = sut.Resolve(linkedDirectory);

            Assert.False(result.IsSuccess);
            Assert.Contains("reparse points", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(linkedDirectory))
            {
                Directory.Delete(linkedDirectory);
            }
        }
    }

    [Fact]
    public void Resolve_rejects_a_top_level_project_file_symbolic_link()
    {
        var linkedTargetRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProjectTargetResolver.Linked.{Guid.NewGuid():N}");
        Directory.CreateDirectory(linkedTargetRoot);
        var targetProjectPath = Path.Combine(linkedTargetRoot, "Calculator.csproj");
        File.WriteAllText(targetProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var linkedProjectPath = Path.Combine(root, "Calculator.csproj");
        File.CreateSymbolicLink(linkedProjectPath, targetProjectPath);
        var sut = CreateSut();

        try
        {
            var result = sut.Resolve(root);

            Assert.False(result.IsSuccess);
            Assert.Contains("reparse points", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(linkedProjectPath))
            {
                File.Delete(linkedProjectPath);
            }

            if (Directory.Exists(linkedTargetRoot))
            {
                Directory.Delete(linkedTargetRoot, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateProject(string directoryName, string fileName)
    {
        var directoryPath = string.IsNullOrWhiteSpace(directoryName)
            ? root
            : Path.Combine(root, directoryName);
        Directory.CreateDirectory(directoryPath);
        var projectPath = Path.Combine(directoryPath, fileName);
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return projectPath;
    }

    private ProjectStructureDotNetProjectTargetResolver CreateSut()
        => new(
            new FileSystemStoragePathPolicy(new LocalWorkspacePathResolver(root)));

    private sealed class LocalWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }
}
