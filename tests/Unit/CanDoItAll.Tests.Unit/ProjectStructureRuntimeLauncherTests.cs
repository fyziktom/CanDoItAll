using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureRuntimeLauncherTests : IDisposable
{
    private readonly string workspaceRoot = TestFileSystem.CreateTemporaryRoot("runtime-launcher");

    public ProjectStructureRuntimeLauncherTests()
    {
        CreateProjectFile("repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj");
        CreateProjectFile("repos/TetrisGame/src/TetrisGame/TetrisGame.csproj");
        CreateFile("scripts/task.ps1", "Write-Output 'ready'");
        CreateFile("repos/python-app/.venv/Scripts/Activate.ps1", "Write-Output 'activated'");
        Directory.CreateDirectory(WorkspacePath("repos/compose-app"));
    }

    [Fact]
    public void Resolve_returns_watch_plan_with_launch_profile_when_configured()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = "repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj",
                LaunchProfileName = "https"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        var projectPath = WorkspacePath("repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj");
        Assert.Equal(Path.GetDirectoryName(projectPath), result.Plan!.WorkingDirectory);
        Assert.Equal("dotnet watch", result.Plan.DisplayName);
        Assert.Equal(
            $"dotnet watch --project '{projectPath}' run --launch-profile 'https'",
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
                ProjectPath = WorkspacePath("repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj"),
                LocalhostUrl = "https://localhost:7271"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Contains("$env:ASPNETCORE_URLS = 'https://localhost:7271'", result.Plan!.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            $"dotnet watch --project '{WorkspacePath("repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj")}' run --no-launch-profile",
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
                ProjectPath = "src/TetrisGame/TetrisGame.csproj",
                WorkingDirectory = "repos/TetrisGame",
                LocalhostUrl = "http://127.0.0.1:55963/"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        var workingDirectory = WorkspacePath("repos/TetrisGame");
        var projectPath = WorkspacePath("repos/TetrisGame/src/TetrisGame/TetrisGame.csproj");
        Assert.Equal(workingDirectory, result.Plan!.WorkingDirectory);
        Assert.Contains("$env:ASPNETCORE_URLS = 'http://127.0.0.1:55963/'", result.Plan.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            $"dotnet run --project '{projectPath}' --no-launch-profile",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_hydrates_dotnet_runtime_from_note_only_command_evidence()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetRuntime,
            new ProjectEnvironmentMetadata(),
            $"Launch the client-only TetrisGame app from {WorkspacePath("repos/TetrisGame")} using `dotnet run --project src/TetrisGame/TetrisGame.csproj`. Observed QA launch returned `http://127.0.0.1:55963/`.");

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(".NET runtime", result.Plan!.DisplayName);
        var workingDirectory = WorkspacePath("repos/TetrisGame");
        var projectPath = WorkspacePath("repos/TetrisGame/src/TetrisGame/TetrisGame.csproj");
        Assert.Equal(workingDirectory, result.Plan.WorkingDirectory);
        Assert.Contains("$env:ASPNETCORE_URLS = 'http://127.0.0.1:55963/'", result.Plan.StartupScript, StringComparison.Ordinal);
        Assert.Equal(
            $"dotnet run --project '{projectPath}' --no-launch-profile",
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
            WorkingDirectory = "repos/python-app"
        });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(WorkspacePath("repos/python-app"), result.Plan!.WorkingDirectory);
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
        Assert.Equal(workspaceRoot, result.Plan!.WorkingDirectory);
        Assert.Equal("PowerShell script", result.Plan.DisplayName);
        Assert.Equal("pwsh ./scripts/task.ps1 -Verbose", result.Plan.DisplayCommand);
        Assert.Contains($"Set-Location -LiteralPath '{workspaceRoot}'", result.Plan.StartupScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_returns_powershell_script_path_plan_when_working_directory_is_omitted()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                ScriptPath = "scripts/task.ps1"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        var scriptPath = WorkspacePath("scripts/task.ps1");
        Assert.Equal(Path.GetDirectoryName(scriptPath), result.Plan!.WorkingDirectory);
        Assert.Equal("PowerShell script", result.Plan.DisplayName);
        Assert.Equal($"& '{scriptPath}'", result.Plan.DisplayCommand);
        Assert.Equal(scriptPath, result.Plan.Target!.Path);
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
            }) with
        {
            MetadataJson =
                """
                {
                  "script": {
                    "command": "npx tailwindcss -i ./input.css -o ./output.css --watch"
                  }
                }
                """
        };

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(workspaceRoot, result.Plan!.WorkingDirectory);
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
                ProjectPath = "repos/python-app",
                PythonProvider = ProjectPythonProvider.Python,
                EnvironmentName = ".venv"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal("Python environment", result.Plan!.DisplayName);
        var projectPath = WorkspacePath("repos/python-app");
        var activationPath = WorkspacePath("repos/python-app/.venv/Scripts/Activate.ps1");
        Assert.Equal(projectPath, result.Plan.WorkingDirectory);
        Assert.Equal(
            $"& '{activationPath}'",
            result.Plan.DisplayCommand);
        Assert.Equal(activationPath, result.Plan.Target!.Path);
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
                ProjectPath = "repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj"
            }) with
        {
            MetadataJson =
                """
                {
                  "environment": {
                    "projectPath": "repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj"
                  }
                }
                """
        };

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal("dotnet watch", result.Plan!.DisplayName);
        Assert.Equal(
            $"dotnet watch --project '{WorkspacePath("repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj")}' run",
            result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_rejects_an_explicit_environment_kind_that_disagrees_with_the_subtype()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.PythonEnvironment,
            "dotnet-watch",
            new ProjectEnvironmentMetadata
            {
                ProjectPath = "repos/CanDoItAll/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj"
            });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not match objectSubtype 'dotnet-watch'", result.Message, StringComparison.Ordinal);
        Assert.Contains("DotNetWatch", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_rejects_a_quoted_direct_dotnet_watch_command_on_a_legacy_script_node()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                ScriptKind = ProjectScriptKind.PowerShell,
                Command = "& \"C:\\Program Files\\dotnet\\dotnet.exe\"",
                Arguments = "watch --project Calculator.csproj run"
            });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Contains("typed Environment node", result.Message, StringComparison.Ordinal);
        Assert.Contains("project_structure_node_update", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_rejects_dotnet_watch_hidden_behind_a_powershell_command_wrapper()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                ScriptKind = ProjectScriptKind.PowerShell,
                Command = "pwsh",
                Arguments = "-NoProfile -Command \"dotnet watch --project Calculator.csproj run\""
            });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Contains("typed Environment node", result.Message, StringComparison.Ordinal);
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
                WorkingDirectory = "repos/compose-app"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(WorkspacePath("repos/compose-app"), result.Plan!.WorkingDirectory);
        Assert.Equal("Docker runtime", result.Plan.DisplayName);
        Assert.Equal("docker compose up --build", result.Plan.DisplayCommand);
        Assert.Equal(WorkspacePath("repos/compose-app"), result.Plan.Target!.Path);
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
                FolderPath = "repos/compose-app"
            });

        var result = sut.Resolve(node);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.Equal(WorkspacePath("repos/compose-app"), result.Plan!.WorkingDirectory);
        Assert.Equal("docker compose up", result.Plan.DisplayCommand);
    }

    [Fact]
    public void Resolve_rejects_a_missing_script_working_directory()
    {
        var result = CreateSut().Resolve(CreateScriptNode(new ProjectScriptMetadata
        {
            ScriptKind = ProjectScriptKind.Console,
            Command = "python",
            Arguments = "app.py",
            WorkingDirectory = "repos/missing-script-app"
        }));

        Assert.False(result.IsSuccess);
        Assert.Equal("Script working directory does not exist or is not accessible.", result.Message);
    }

    [Fact]
    public void Resolve_rejects_a_file_used_as_the_relative_project_path_base()
    {
        var workingDirectoryFile = WorkspacePath("runtime-base.txt");
        File.WriteAllText(workingDirectoryFile, "not a directory");
        var result = CreateSut().Resolve(CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetRuntime,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = "src/Calculator.csproj",
                WorkingDirectory = workingDirectoryFile
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Runtime working directory must be a directory, but the configured path is a file.",
            result.Message);
    }

    [Fact]
    public void Resolve_rejects_a_missing_powershell_script_target()
    {
        var result = CreateSut().Resolve(CreateScriptNode(
            "powershell",
            new ProjectScriptMetadata
            {
                ScriptPath = "scripts/missing.ps1"
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "The configured script path does not exist, has the wrong path type, or is not accessible.",
            result.Message);
    }

    [Fact]
    public void Resolve_rejects_a_missing_docker_working_directory()
    {
        var result = CreateSut().Resolve(CreateInfrastructureNode(
            "docker-mode",
            new ProjectInfrastructureMetadata
            {
                InfrastructureKind = ProjectInfrastructureKind.DockerMode,
                RuntimeCommand = "docker compose up",
                WorkingDirectory = "repos/missing-compose-app"
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal("Docker working directory does not exist or is not accessible.", result.Message);
    }

    [Fact]
    public void Resolve_rejects_a_missing_python_project_directory()
    {
        var result = CreateSut().Resolve(CreateEnvironmentNode(
            ProjectEnvironmentKind.PythonEnvironment,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = "repos/missing-python-app",
                PythonProvider = ProjectPythonProvider.Conda,
                EnvironmentName = "calculator"
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal("Python project path does not exist or is not accessible.", result.Message);
    }

    [Fact]
    public void Resolve_fails_when_script_command_and_script_path_are_missing()
    {
        var sut = CreateSut();
        var node = CreateScriptNode(new ProjectScriptMetadata
        {
            ScriptKind = ProjectScriptKind.Console,
            WorkingDirectory = "repos/python-app"
        });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal("Script launch requires a command or PowerShell script path.", result.Message);
    }

    [Fact]
    public void Resolve_returns_a_diagnostic_for_malformed_legacy_runtime_metadata()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = "repos/Calculator/Calculator.csproj"
            }) with
        {
            MetadataJson = "{"
        };

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Runtime metadata is invalid and must be repaired before this node can be launched.",
            result.Message);
    }

    public void Dispose()
        => TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);

    private ProjectStructureRuntimeLauncher CreateSut()
        => new(
            new WorkspacePathAccessGuard(
                new TestWorkspacePathResolver(workspaceRoot),
                TestWorkspaceServices.PhysicalPathPolicyFactory),
            NullLogger<ProjectStructureRuntimeLauncher>.Instance,
            new ExistingProjectTargetResolver(),
            new ExternalTargetPathRegistryFactory(),
            new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(workspaceRoot)));

    private string WorkspacePath(string relativePath)
        => TestRepositoryPath.Resolve(workspaceRoot, relativePath);

    private void CreateProjectFile(string relativePath)
        => CreateFile(relativePath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

    private void CreateFile(string relativePath, string content)
    {
        var path = WorkspacePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

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

    private sealed class ExistingProjectTargetResolver : IProjectStructureDotNetProjectTargetResolver
    {
        public ProjectStructureDotNetProjectTargetResolution Resolve(string path)
            => new(path, "Verified by the launcher test boundary.");
    }
}
