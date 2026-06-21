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
    public void Resolve_uses_dotnet_working_directory_for_relative_project_paths()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetRuntime,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"src\TetrisGame\TetrisGame.csproj",
                WorkingDirectory = @"repos\TetrisGame",
                LocalhostUrl = "http://127.0.0.1:55963/"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\repos\TetrisGame", result.Plan!.WorkingDirectory);
        Assert.Contains("$env:ASPNETCORE_URLS = 'http://127.0.0.1:55963/'", result.Plan.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            "dotnet run --project 'C:\\workspace\\repos\\TetrisGame\\src\\TetrisGame\\TetrisGame.csproj' --no-launch-profile",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_hydrates_dotnet_runtime_from_note_only_command_evidence()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetRuntime,
            new ProjectEnvironmentMetadata(),
            "Launch the client-only TetrisGame app from C:\\workspace\\repos\\TetrisGame using `dotnet run --project src/TetrisGame/TetrisGame.csproj`. Observed QA launch returned `http://127.0.0.1:55963/`.");

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(".NET runtime", result.Plan!.DisplayName);
        Assert.Equal(@"C:\workspace\repos\TetrisGame", result.Plan.WorkingDirectory);
        Assert.Contains("$env:ASPNETCORE_URLS = 'http://127.0.0.1:55963/'", result.Plan.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            "dotnet run --project 'C:\\workspace\\repos\\TetrisGame\\src\\TetrisGame\\TetrisGame.csproj' --no-launch-profile",
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
    public void Resolve_returns_powershell_command_plan_when_working_directory_is_omitted()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                Command = "pwsh ./scripts/task.ps1",
                Arguments = "-Verbose"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace", result.Plan!.WorkingDirectory);
        Assert.Equal("PowerShell script", result.Plan.DisplayName);
        Assert.Equal("pwsh ./scripts/task.ps1 -Verbose", result.Plan.DisplayCommand);
        Assert.Contains("Set-Location -LiteralPath 'C:\\workspace'", result.Plan.StartupScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_returns_powershell_script_path_plan_when_working_directory_is_omitted()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                ScriptPath = @"scripts\task.ps1"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\scripts", result.Plan!.WorkingDirectory);
        Assert.Equal("PowerShell script", result.Plan.DisplayName);
        Assert.Equal("& 'C:\\workspace\\scripts\\task.ps1'", result.Plan.DisplayCommand);
        Assert.Equal(@"C:\workspace\scripts\task.ps1", result.Plan.Target!.Path);
    }

    [Fact]
    public void Resolve_uses_script_subtype_when_kind_metadata_is_missing()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "tailwind-watch",
            new ProjectScriptMetadata
            {
                Command = "npx tailwindcss -i ./input.css -o ./output.css --watch"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace", result.Plan!.WorkingDirectory);
        Assert.Equal("Tailwind watch", result.Plan.DisplayName);
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
    public void Resolve_uses_environment_subtype_when_kind_metadata_is_missing()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.PythonEnvironment,
            "dotnet-watch",
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal("dotnet watch", result.Plan!.DisplayName);
        Assert.Equal(
            "dotnet watch --project 'C:\\workspace\\repos\\CanDoItAll\\src\\CanDoItAll.Web\\CanDoItAll.Web.csproj' run",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_returns_docker_runtime_plan_from_command_and_working_directory()
    {
        var sut = CreateSut();
        var node = CreateInfrastructureNode(
            "docker-mode",
            new ProjectInfrastructureMetadata
            {
                InfrastructureKind = ProjectInfrastructureKind.DockerMode,
                RuntimeCommand = "docker compose up",
                RuntimeArguments = "--build",
                WorkingDirectory = @"repos\compose-app"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\repos\compose-app", result.Plan!.WorkingDirectory);
        Assert.Equal("Docker runtime", result.Plan.DisplayName);
        Assert.Equal("docker compose up --build", result.Plan.DisplayCommand);
        Assert.Equal(@"C:\workspace\repos\compose-app", result.Plan.Target!.Path);
        Assert.True(result.Plan.Target.IsDirectory);
    }

    [Fact]
    public void Resolve_uses_docker_folder_path_when_working_directory_is_missing()
    {
        var sut = CreateSut();
        var node = CreateInfrastructureNode(
            "docker-mode",
            new ProjectInfrastructureMetadata
            {
                InfrastructureKind = ProjectInfrastructureKind.DockerMode,
                RuntimeCommand = "docker compose",
                RuntimeArguments = "up",
                FolderPath = @"repos\compose-app"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(@"C:\workspace\repos\compose-app", result.Plan!.WorkingDirectory);
        Assert.Equal("docker compose up", result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_fails_when_script_command_and_script_path_are_missing()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(new ProjectScriptMetadata
        {
            ScriptKind = ProjectScriptKind.Console,
            WorkingDirectory = @"repos\python-app"
        });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal("Script launch requires a command or PowerShell script path.", result.Message);
    }

    private static ProjectStructureRuntimeLauncher CreateSut()
        => new(
            new WorkspacePathAccessGuard(new TestWorkspacePathResolver(@"C:\workspace")),
            NullLogger<ProjectStructureRuntimeLauncher>.Instance);

    private static ProjectStructureNode CreateEnvironmentNode(ProjectEnvironmentKind kind, ProjectEnvironmentMetadata metadata, string notes = "")
        => CreateEnvironmentNode(kind, kind switch
        {
            ProjectEnvironmentKind.DotNetWatch => "dotnet-watch",
            ProjectEnvironmentKind.DotNetRelease => "dotnet-release",
            ProjectEnvironmentKind.PythonEnvironment => "python",
            _ => "dotnet-runtime"
        }, metadata, notes);

    private static ProjectStructureNode CreateEnvironmentNode(ProjectEnvironmentKind kind, string objectSubtype, ProjectEnvironmentMetadata metadata, string notes = "")
    {
        metadata.EnvironmentKind = kind;
        return CreateNode(ProjectObjectType.Environment, objectSubtype, new ProjectObjectMetadataEnvelope
        {
            Environment = metadata
        }, notes);
    }

    private static ProjectStructureNode CreateScriptNode(ProjectScriptMetadata metadata)
        => CreateNode(ProjectObjectType.Script, "console", new ProjectObjectMetadataEnvelope
        {
            Script = metadata
        });

    private static ProjectStructureNode CreateScriptNode(string objectSubtype, ProjectScriptMetadata metadata)
        => CreateNode(ProjectObjectType.Script, objectSubtype, new ProjectObjectMetadataEnvelope
        {
            Script = metadata
        });

    private static ProjectStructureNode CreateInfrastructureNode(string objectSubtype, ProjectInfrastructureMetadata metadata)
        => CreateNode(ProjectObjectType.Infrastructure, objectSubtype, new ProjectObjectMetadataEnvelope
        {
            Infrastructure = metadata
        });

    private static ProjectStructureNode CreateNode(ProjectObjectType objectType, string objectSubtype, ProjectObjectMetadataEnvelope metadata, string notes = "")
        => new(
            "node-1",
            "project:1",
            objectType,
            objectSubtype,
            "Runtime node",
            "Context",
            "Planned",
            notes,
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
            [],
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
