using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureRuntimeLauncherTests
{
    [Fact]
    public void Resolve_returns_watch_plan_with_launch_profile_when_configured()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
                LaunchProfileName = "https"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\repos\CanDoItAll\src\CanDoItAll.Web", result.Plan!.WorkingDirectory);
        Assert.Equal("dotnet watch", result.Plan.DisplayName);
        Assert.Equal(
            "dotnet watch --project 'C:\\workspace\\repos\\CanDoItAll\\src\\CanDoItAll.Web\\CanDoItAll.Web.csproj' run --launch-profile 'https'",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_prefers_explicit_urls_for_watch_launches()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"C:\workspace\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
                LocalhostUrl = "https://localhost:7271"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Contains("$env:ASPNETCORE_URLS = 'https://localhost:7271'", result.Plan!.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            "dotnet watch --project 'C:\\workspace\\repos\\CanDoItAll\\src\\CanDoItAll.Web\\CanDoItAll.Web.csproj' run --no-launch-profile",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_returns_console_script_plan_from_command_and_arguments()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(new ProjectScriptMetadata
        {
            ScriptKind = ProjectScriptKind.Console,
            Command = "python",
            Arguments = "app.py --watch",
            WorkingDirectory = @"repos\python-app"
        });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\repos\python-app", result.Plan!.WorkingDirectory);
        Assert.Equal("python app.py --watch", result.Plan.DisplayCommand);
        Assert.Equal("script command", result.Plan.DisplayName);
    }

    [Fact]
    public void Resolve_returns_python_activation_plan_for_virtual_environment_nodes()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.PythonEnvironment,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"repos\python-app",
                PythonProvider = ProjectPythonProvider.Python,
                EnvironmentName = ".venv"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal("Python environment", result.Plan!.DisplayName);
        Assert.Equal(@"C:\workspace\repos\python-app", result.Plan.WorkingDirectory);
        Assert.Equal(
            "& 'C:\\workspace\\repos\\python-app\\.venv\\Scripts\\Activate.ps1'",
            result.Plan.DisplayCommand);
        Assert.Equal(@"C:\workspace\repos\python-app\.venv\Scripts\Activate.ps1", result.Plan.Target!.Path);
    }

    [Fact]
    public void Resolve_fails_when_script_working_directory_is_missing()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(new ProjectScriptMetadata
        {
            ScriptKind = ProjectScriptKind.Console,
            Command = "npm run dev"
        });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal("Script launch requires a working directory.", result.Message);
    }

    private static ProjectStructureRuntimeLauncher CreateSut()
        => new(
            new WorkspacePathAccessGuard(new TestWorkspacePathResolver(@"C:\workspace")),
            NullLogger<ProjectStructureRuntimeLauncher>.Instance);

    private static ProjectStructureNode CreateEnvironmentNode(ProjectEnvironmentKind kind, ProjectEnvironmentMetadata metadata)
    {
        metadata.EnvironmentKind = kind;
        return CreateNode(ProjectObjectType.Environment, kind switch
        {
            ProjectEnvironmentKind.DotNetWatch => "dotnet-watch",
            ProjectEnvironmentKind.DotNetRelease => "dotnet-release",
            ProjectEnvironmentKind.PythonEnvironment => "python",
            _ => "dotnet-runtime"
        }, new ProjectObjectMetadataEnvelope
        {
            Environment = metadata
        });
    }

    private static ProjectStructureNode CreateScriptNode(ProjectScriptMetadata metadata)
        => CreateNode(ProjectObjectType.Script, "console", new ProjectObjectMetadataEnvelope
        {
            Script = metadata
        });

    private static ProjectStructureNode CreateNode(ProjectObjectType objectType, string objectSubtype, ProjectObjectMetadataEnvelope metadata)
        => new(
            "node-1",
            "project:1",
            objectType,
            objectSubtype,
            "Runtime node",
            "Context",
            "Planned",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "terminal", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            null,
            null,
            ProjectObjectMetadataSerializer.Serialize(metadata));

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
