using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRuntimeProcessToolsTests
{
    [Fact]
    public void BuildWatchArgumentList_uses_launch_profile_by_default()
    {
        var options = new ManagerOptions();

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(@"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj", options);

        Assert.Equal(
            [
                "watch",
                "--non-interactive",
                "--project",
                @"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
                "--no-restore",
                "--disable-build-servers",
                "run",
                "--launch-profile",
                "https"
            ],
            arguments);
    }

    [Fact]
    public void BuildWatchArgumentList_uses_no_launch_profile_when_explicit_urls_are_configured()
    {
        var options = new ManagerOptions
        {
            WatchUrls = ["https://127.0.0.1:0", "http://127.0.0.1:0"]
        };

        var arguments = WorkspaceRuntimeProcessTools.BuildWatchArgumentList(@"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj", options);

        Assert.Equal(
            [
                "watch",
                "--non-interactive",
                "--project",
                @"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
                "--no-restore",
                "--disable-build-servers",
                "run",
                "--no-launch-profile"
            ],
            arguments);
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

    [Theory]
    [InlineData("System.IO.IOException: Failed to bind to address https://127.0.0.1:7271: address already in use.")]
    [InlineData("CSC : error CS2012: Cannot open 'ComponentKit.dll' for writing -- The requested operation cannot be performed on a file with a user-mapped section open.")]
    [InlineData(@"error MSB3021: Unable to copy file ""apphost.exe"" to ""CanDoItAll.Web.exe"". The process cannot access the file because it is being used by another process.")]
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
}
