using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureRuntimePlanCompilerTests
{
    private readonly ProjectStructureRuntimePlanCompiler compiler = new();

    [Fact]
    public void Compile_dotnet_watch_produces_typed_arguments_and_environment_without_shell_text()
    {
        var target = new ProjectStructureRuntimeLaunchTarget("project file", "/repo/App.csproj", false);
        var result = compiler.Compile(
            new ProjectStructureDotNetRuntimeDefinition(
                "/repo",
                "dotnet watch",
                target,
                "/repo/App.csproj",
                IsWatch: true,
                IsRelease: false,
                LaunchProfileName: string.Empty,
                LocalhostUrl: "http://127.0.0.1:5032"),
            ProjectStructureRuntimeHostPlatform.Linux);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Plan);
        Assert.Equal(ProjectStructureRuntimePlanKind.DotNet, result.Plan!.Kind);
        Assert.Equal(["dotnet"], result.Plan.ExecutableCandidates);
        Assert.Equal(
            ["watch", "--project", "/repo/App.csproj", "run", "--no-launch-profile"],
            result.Plan.Arguments);
        Assert.Equal("http://127.0.0.1:5032", result.Plan.EnvironmentVariables["ASPNETCORE_URLS"]);
        Assert.DoesNotContain("powershell", result.Plan.DisplayCommand, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProjectStructureRuntimeHostPlatform.Windows, "C:\\repo\\.venv", "C:\\repo\\.venv\\Scripts\\python.exe")]
    [InlineData(ProjectStructureRuntimeHostPlatform.Linux, "/repo/.venv", "/repo/.venv/bin/python")]
    [InlineData(ProjectStructureRuntimeHostPlatform.MacOS, "/repo/.venv", "/repo/.venv/bin/python")]
    public void Compile_python_virtual_environment_uses_the_host_specific_interpreter_layout(
        ProjectStructureRuntimeHostPlatform platform,
        string environmentPath,
        string expectedInterpreter)
    {
        var result = compiler.Compile(
            new ProjectStructurePythonRuntimeDefinition(
                "/repo",
                "Python environment",
                new ProjectStructureRuntimeLaunchTarget("Python project path", "/repo/app.py", false),
                ProjectPythonProvider.Python,
                environmentPath,
                "ignored-for-venv",
                "/repo/app.py",
                ["--watch"]),
            platform);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Plan);
        Assert.Equal([expectedInterpreter], result.Plan!.ExecutableCandidates);
        Assert.Equal(["/repo/app.py", "--watch"], result.Plan.Arguments);
        Assert.DoesNotContain("activate", result.Plan.DisplayCommand, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMigrate_unwraps_a_bounded_static_cmd_command_into_executable_and_arguments()
    {
        var result = ProjectStructureLegacyRuntimeCommandMigrator.TryMigrate(
            "cmd.exe",
            "/c \"docker compose up --build\"");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("docker", result.Executable);
        Assert.Equal(["compose", "up", "--build"], result.Arguments);
    }

    [Theory]
    [InlineData("powershell.exe", "-EncodedCommand ZABvAHQAbgBlAHQA")]
    [InlineData("cmd.exe", "/c \"docker compose up && whoami\"")]
    [InlineData("pwsh", "-Command \"$tool = 'dotnet'; & $tool run\"")]
    [InlineData("pwsh", "-File other.ps1 -Command \"dotnet run\"")]
    [InlineData("pwsh", "-WorkingDirectory C:\\repo -Command \"dotnet run\"")]
    [InlineData("pwsh", "-NoProfile -Command -Command \"dotnet run\"")]
    public void TryMigrate_requires_operator_repair_for_encoded_dynamic_or_chained_shell_content(
        string command,
        string arguments)
    {
        var result = ProjectStructureLegacyRuntimeCommandMigrator.TryMigrate(command, arguments);

        Assert.False(result.IsSuccess);
        Assert.Contains("operator repair", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMigrate_accepts_only_bounded_flag_only_PowerShell_host_options_before_command()
    {
        var result = ProjectStructureLegacyRuntimeCommandMigrator.TryMigrate(
            "pwsh",
            "-NoLogo -NoProfile -NonInteractive -Command \"docker compose up\"");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("docker", result.Executable);
        Assert.Equal(["compose", "up"], result.Arguments);
    }
}
