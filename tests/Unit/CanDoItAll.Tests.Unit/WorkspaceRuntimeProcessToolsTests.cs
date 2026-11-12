using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceRuntimeProcessToolsTests
{
    [Fact]
    public void BuildWatchArgumentList_uses_launch_profile_by_default_when_restore_inputs_are_fresh()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="../../Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """,
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateProject(
            @"src\Foundation\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj",
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
            @"src\App\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\Foundation\CanDoItAll.Infrastructure\obj\project.assets.json",
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
            @"src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\App\CanDoItAll.Web\obj\project.assets.json",
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
            @"src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\App\CanDoItAll.Web\obj\project.assets.json",
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
            @"src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
              <ProjectReference Include="../../Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """,
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateProject(
            @"src\Foundation\CanDoItAll.Infrastructure\CanDoItAll.Infrastructure.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 6, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\App\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\Foundation\CanDoItAll.Infrastructure\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, new ManagerOptions());

        Assert.DoesNotContain("--no-restore", arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_omits_no_restore_when_central_restore_inputs_changed_after_assets()
    {
        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\App\CanDoItAll.Web\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        workspace.CreateFile(
            "Directory.Packages.props",
            "<Project />",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 6, 0, DateTimeKind.Utc));

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspace.RootPath, projectPath, new ManagerOptions());

        Assert.DoesNotContain("--no-restore", arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_stops_at_case_variant_workspace_root_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TestWorkspace();
        var projectPath = workspace.CreateProject(
            @"src\App\App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc));
        workspace.CreateAssetsFile(
            @"src\App\obj\project.assets.json",
            modifiedAtUtc: new DateTime(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc));
        var outsideRestoreInput = Path.Combine(Path.GetDirectoryName(workspace.RootPath)!, "Directory.Packages.props");
        File.WriteAllText(outsideRestoreInput, "<Project />");
        File.SetLastWriteTimeUtc(outsideRestoreInput, new DateTime(2026, 3, 20, 8, 10, 0, DateTimeKind.Utc));

        try
        {
            var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(
                workspace.RootPath.ToUpperInvariant(),
                projectPath,
                new ManagerOptions());

            Assert.Contains("--no-restore", arguments);
        }
        finally
        {
            File.Delete(outsideRestoreInput);
        }
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
    public void ResolveTailwindCliShimPath_points_to_the_npm_bin_shim_that_package_scripts_run()
    {
        var tailwindWorkspacePath = Path.Combine(Path.GetTempPath(), "CanDoItAll", "Tailwind");
        var path = WorkspaceRuntimeProcessTools.ResolveTailwindCliShimPath(tailwindWorkspacePath);

        Assert.Equal(
            Path.Combine(
                tailwindWorkspacePath,
                "node_modules",
                ".bin",
                OperatingSystem.IsWindows() ? "tailwindcss.cmd" : "tailwindcss"),
            path);
    }

    [Fact]
    public void CreateNpmCommandPlan_runs_the_npm_cli_installed_beside_the_npm_launcher_through_node()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var npmPath = CreateFile(root, OperatingSystem.IsWindows() ? "npm.cmd" : "npm");
            var npmCliPath = CreateFile(root, "node_modules", "npm", "bin", "npm-cli.js");
            var nodePath = Path.Combine(root, OperatingSystem.IsWindows() ? "node.exe" : "node");

            var plan = WorkspaceRuntimeProcessTools.CreateNpmCommandPlan(nodePath, npmPath, ["run", "watch"]);

            Assert.Equal(nodePath, plan.ExecutablePath);
            Assert.Equal([npmCliPath, "run", "watch"], plan.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateNpmCommandPlan_finds_the_npm_cli_in_a_unix_style_prefix_layout()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var npmPath = CreateFile(root, "bin", OperatingSystem.IsWindows() ? "npm.cmd" : "npm");
            var npmCliPath = CreateFile(root, "lib", "node_modules", "npm", "bin", "npm-cli.js");
            var nodePath = Path.Combine(root, "bin", OperatingSystem.IsWindows() ? "node.exe" : "node");

            var plan = WorkspaceRuntimeProcessTools.CreateNpmCommandPlan(nodePath, npmPath, ["install"]);

            Assert.Equal(nodePath, plan.ExecutablePath);
            Assert.Equal([npmCliPath, "install"], plan.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateNpmCommandPlan_follows_a_unix_npm_symlink_to_its_cli_script()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTemporaryDirectory();
        try
        {
            var npmCliPath = CreateFile(root, "toolchain", "npm-cli.js");
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            var npmPath = Path.Combine(root, "bin", "npm");
            File.CreateSymbolicLink(npmPath, npmCliPath);

            var plan = WorkspaceRuntimeProcessTools.CreateNpmCommandPlan("/usr/bin/node", npmPath, ["run", "watch"]);

            Assert.Equal("/usr/bin/node", plan.ExecutablePath);
            Assert.EndsWith(Path.Combine("toolchain", "npm-cli.js"), plan.Arguments[0], StringComparison.Ordinal);
            Assert.True(File.Exists(plan.Arguments[0]));
            Assert.Equal(["run", "watch"], plan.Arguments.Skip(1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateNpmCommandPlan_requires_the_npm_cli_on_windows_and_runs_npm_directly_elsewhere()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var npmPath = CreateFile(root, OperatingSystem.IsWindows() ? "npm.cmd" : "npm");
            var nodePath = Path.Combine(root, OperatingSystem.IsWindows() ? "node.exe" : "node");

            if (OperatingSystem.IsWindows())
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => WorkspaceRuntimeProcessTools.CreateNpmCommandPlan(nodePath, npmPath, ["run", "watch"]));
                Assert.Contains("npm-cli.js", exception.Message, StringComparison.Ordinal);
                return;
            }

            var plan = WorkspaceRuntimeProcessTools.CreateNpmCommandPlan(nodePath, npmPath, ["run", "watch"]);

            Assert.Equal(npmPath, plan.ExecutablePath);
            Assert.Equal(["run", "watch"], plan.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "CanDoItAll.Manager.NpmPlan", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateFile(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Theory]
    [InlineData("System.IO.IOException: Failed to bind to address https://127.0.0.1:7271: address already in use.")]
    [InlineData("CSC : error CS2012: Cannot open 'ComponentKit.dll' for writing -- The requested operation cannot be performed on a file with a user-mapped section open.")]
    [InlineData("error M" + "SB" + @"3021: Unable to copy file ""apphost.exe"" to ""CanDoItAll.Web.exe"". The process cannot access the file because it is being used by another process.")]
    public void RequiresWorkspaceRecovery_detects_lock_and_port_conflicts(string line)
    {
        Assert.True(WorkspaceRuntimeProcessTools.RequiresWorkspaceRecovery(line));
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
            var fullPath = TestRepositoryPath.Resolve(RootPath, relativePath);
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
