using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRuntimeProcessToolsTests
{
    [Fact]
    public void BuildWatchArgumentList_uses_launch_profile_by_default_when_restore_inputs_are_fresh()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="..\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """,
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateProject(
            @"src\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
            </Project>
            """,
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 1, 0, DateTimeKind.Utc));
        workspace.CreateFile(
            "Directory.Packages.props",
            "<Project />",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 2, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Infrastructure\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));

        var options = new ManagerOptions();
        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, options);

        Assert.Equal(
            [
                "watch",
                "--non-interactive",
                "--project",
                projectPath,
                "--no-restore",
                "run",
                "--launch-profile",
                "https"
            ],
            arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_uses_no_launch_profile_when_explicit_urls_are_configured()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));

        var options = new ManagerOptions
        {
            WatchUrls = ["https://127.0.0.1:0", "http://127.0.0.1:0"]
        };

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, options);

        Assert.Equal(
            [
                "watch",
                "--non-interactive",
                "--project",
                projectPath,
                "--no-restore",
                "run",
                "--no-launch-profile"
            ],
            arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_includes_disable_build_servers_when_requested()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(
            workspace.RootPath,
            projectPath,
            new ManagerOptions { WatchDisableBuildServers = true });

        Assert.Contains("--disable-build-servers", arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_omits_no_restore_when_a_referenced_project_assets_file_is_stale()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="..\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """,
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateProject(
            @"src\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 6, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Infrastructure\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, new ManagerOptions());

        Assert.DoesNotContain("--no-restore", arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_omits_no_restore_when_central_restore_inputs_changed_after_assets()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateFile(
            "Directory.Packages.props",
            "<Project />",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 6, 0, DateTimeKind.Utc));

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, new ManagerOptions());

        Assert.DoesNotContain("--no-restore", arguments);
    }

    [Fact]
    public void BuildWatchUrlsEnvironmentValue_returns_null_when_no_explicit_urls_are_configured()
    {
        var value = WorkspaceRuntimeProcessTools.BuildWatchUrlsEnvironmentValue(new ManagerOptions());

        Assert.Null(value);
    }

    [Fact]
    public void BuildWatchUrlsEnvironmentValue_joins_explicit_urls_for_aspnetcore_urls()
    {
        var options = new ManagerOptions
        {
            WatchUrls = ["https://127.0.0.1:0", "http://127.0.0.1:0"]
        };

        var value = WorkspaceRuntimeProcessTools.BuildWatchUrlsEnvironmentValue(options);

        Assert.Equal("https://127.0.0.1:0;http://127.0.0.1:0", value);
    }

    [Fact]
    public void BuildWatchEnvironmentVariables_uses_fast_defaults()
    {
        var variables = WorkspaceRuntimeProcessTools.BuildWatchEnvironmentVariables(new ManagerOptions(), "Development");

        Assert.Equal("1", variables["DOTNET_WATCH_SUPPRESS_EMOJIS"]);
        Assert.Equal("Development", variables["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("Development", variables["DOTNET_ENVIRONMENT"]);
        Assert.Equal("false", variables["UseAppHost"]);
        Assert.Equal("true", variables["DetailedErrors"]);
        Assert.Equal("true", variables["ASPNETCORE_DETAILEDERRORS"]);
        Assert.DoesNotContain("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH", variables.Keys);
        Assert.DoesNotContain("DOTNET_CLI_USE_MSBUILD_SERVER", variables.Keys);
        Assert.DoesNotContain("UseSharedCompilation", variables.Keys);
        Assert.DoesNotContain("ASPNETCORE_URLS", variables.Keys);
    }

    [Fact]
    public void BuildWatchEnvironmentVariables_honors_opt_in_slow_overrides()
    {
        var variables = WorkspaceRuntimeProcessTools.BuildWatchEnvironmentVariables(
            new ManagerOptions
            {
                WatchDisableBuildServers = true,
                WatchDisableSharedCompilation = true,
                WatchSuppressBrowserRefresh = true,
                WatchUrls = ["https://127.0.0.1:7271", "http://127.0.0.1:5032"]
            },
            "Development");

        Assert.Equal("1", variables["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"]);
        Assert.Equal("0", variables["DOTNET_CLI_USE_MSBUILD_SERVER"]);
        Assert.Equal("false", variables["UseSharedCompilation"]);
        Assert.Equal("https://127.0.0.1:7271;http://127.0.0.1:5032", variables["ASPNETCORE_URLS"]);
    }

    [Fact]
    public void BuildTailwindWatchArgumentList_targets_input_and_output_files()
    {
        var arguments = WorkspaceRuntimeProcessTools.BuildTailwindWatchArgumentList(
            @"C:\repos\CanDoItAll\Tailwind\input.css",
            @"C:\repos\CanDoItAll\src\CanDoItAll.Web\wwwroot\css\output.css");

        Assert.Equal(
            [
                "-i",
                @"C:\repos\CanDoItAll\Tailwind\input.css",
                "-o",
                @"C:\repos\CanDoItAll\src\CanDoItAll.Web\wwwroot\css\output.css",
                "--watch=always"
            ],
            arguments);
    }

    [Fact]
    public void BuildTailwindBuildArgumentList_targets_input_and_output_files_without_watch_mode()
    {
        var arguments = WorkspaceRuntimeProcessTools.BuildTailwindBuildArgumentList(
            @"Tailwind\input.css",
            @"..\src\CanDoItAll.Web\wwwroot\css\output.css");

        Assert.Equal(
            [
                "-i",
                @"Tailwind\input.css",
                "-o",
                @"..\src\CanDoItAll.Web\wwwroot\css\output.css"
            ],
            arguments);
    }

    [Fact]
    public void ResolveTailwindCliPath_points_to_workspace_local_binary()
    {
        var path = WorkspaceRuntimeProcessTools.ResolveTailwindCliPath(@"C:\repos\CanDoItAll\Tailwind");

        Assert.EndsWith(@"Tailwind\node_modules\.bin\tailwindcss.cmd", path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("System.IO.IOException: Failed to bind to address https://127.0.0.1:7271: address already in use.")]
    [InlineData("CSC : error CS2012: Cannot open 'ComponentKit.dll' for writing -- The requested operation cannot be performed on a file with a user-mapped section open.")]
    [InlineData("error M" + "SB" + @"3021: Unable to copy file ""apphost.exe"" to ""CanDoItAll.Web.exe"". The process cannot access the file because it is being used by another process.")]
    public void RequiresWorkspaceRecovery_detects_lock_and_port_conflicts(string line)
    {
        Assert.True(WorkspaceRuntimeProcessTools.RequiresWorkspaceRecovery(line));
    }

    [Fact]
    public void IsWorkspaceOwnedProcess_matches_watch_host_and_child_processes()
    {
        const string projectPath = @"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj";

        var watchHost = new WorkspaceProcessSnapshot(
            101,
            "dotnet.exe",
            @"""C:\Program Files\dotnet\dotnet.exe"" watch --project ""C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj"" run --no-launch-profile",
            null);

        var watchChild = new WorkspaceProcessSnapshot(
            102,
            "dotnet.exe",
            @"""C:\Program Files\dotnet\dotnet.exe"" run --no-build -e DOTNET_WATCH=1 --project C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            null);

        var webProcess = new WorkspaceProcessSnapshot(
            103,
            "CanDoItAll.Web.exe",
            @"""C:\repos\CanDoItAll\src\CanDoItAll.Web\bin\Debug\net10.0\CanDoItAll.Web.exe""",
            @"C:\repos\CanDoItAll\src\CanDoItAll.Web\bin\Debug\net10.0\CanDoItAll.Web.exe");

        var unrelated = new WorkspaceProcessSnapshot(
            104,
            "dotnet.exe",
            @"""C:\Program Files\dotnet\dotnet.exe"" build C:\repos\Other\Other.csproj",
            null);

        Assert.True(WorkspaceRuntimeProcessTools.IsWorkspaceOwnedProcess(watchHost, projectPath));
        Assert.True(WorkspaceRuntimeProcessTools.IsWorkspaceOwnedProcess(watchChild, projectPath));
        Assert.True(WorkspaceRuntimeProcessTools.IsWorkspaceOwnedProcess(webProcess, projectPath));
        Assert.False(WorkspaceRuntimeProcessTools.IsWorkspaceOwnedProcess(unrelated, projectPath));
    }

    private sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string CreateProject(string relativePath, string content, DateTime modifiedAtUtc)
            => CreateFile(relativePath, content, modifiedAtUtc);

        public string CreateAssetsFile(string relativePath, DateTime modifiedAtUtc)
            => CreateFile(relativePath, "{}", modifiedAtUtc);

        public string CreateFile(string relativePath, string content, DateTime modifiedAtUtc)
        {
            var fullPath = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            File.SetLastWriteTimeUtc(fullPath, modifiedAtUtc);
            return fullPath;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
