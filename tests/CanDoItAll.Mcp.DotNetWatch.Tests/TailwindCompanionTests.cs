using Microsoft.Extensions.Options;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class TailwindCompanionTests
{
    [Fact]
    public void BuildDefaultEnvironment_DoesNotSuppressBrowserRefresh_WhenDisabled()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.WatchRun,
            "Debug",
            Framework: null,
            LaunchProfile: null,
            Arguments: [],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: ["http://127.0.0.1:5032"]);

        var environment = AppRuntimeManager.BuildDefaultEnvironment(template, usePollingWatcher: false, suppressBrowserRefresh: false);

        Assert.DoesNotContain("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH", environment.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("http://127.0.0.1:5032", environment["ASPNETCORE_URLS"]);
    }

    [Fact]
    public void BuildDefaultEnvironment_SuppressesBrowserRefresh_WhenEnabled()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.WatchRun,
            "Debug",
            Framework: null,
            LaunchProfile: null,
            Arguments: [],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: []);

        var environment = AppRuntimeManager.BuildDefaultEnvironment(template, usePollingWatcher: true, suppressBrowserRefresh: true);

        Assert.Equal("1", environment["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"]);
        Assert.Equal("1", environment["DOTNET_USE_POLLING_FILE_WATCHER"]);
    }

    [Fact]
    public void TailwindWorkspaceDetector_DetectsWorkspaceAndSourceRoots()
    {
        using var workspace = new TailwindWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "SampleApp", "SampleApp.csproj"), "<Project />");
        workspace.WriteFile(
            Path.Combine("Tailwind", "package.json"),
            """
            {
              "name": "tailwind-app",
              "private": true,
              "devDependencies": {
                "@tailwindcss/cli": "^4.1.7",
                "tailwindcss": "^4.1.7"
              },
              "scripts": {
                "build": "tailwindcss -i ./input.css -o ../src/SampleApp/wwwroot/css/output.css",
                "watch": "tailwindcss -i ./input.css -o ../src/SampleApp/wwwroot/css/output.css --watch"
              }
            }
            """);
        workspace.WriteFile(
            Path.Combine("Tailwind", "input.css"),
            """
            @import "./styles/base.css";
            @source "../src/SharedUi";
            """);
        workspace.WriteFile(Path.Combine("Tailwind", "styles", "base.css"), ".card { display: grid; }");
        workspace.WriteFile(Path.Combine("src", "SharedUi", "Card.razor"), "<div class=\"card\"></div>");

        var projectPath = Path.Combine(workspace.RootPath, "src", "SampleApp", "SampleApp.csproj");
        var workingDirectory = Path.Combine(workspace.RootPath, "src", "SampleApp");

        var plan = TailwindWorkspaceDetector.TryDetect(projectPath, workingDirectory);

        Assert.NotNull(plan);
        Assert.Equal(Path.Combine(workspace.RootPath, "Tailwind"), plan!.PackageDirectory);
        Assert.Equal(Path.Combine(workspace.RootPath, "Tailwind", "input.css"), plan.InputPath);
        Assert.Equal(Path.Combine(workspace.RootPath, "src", "SampleApp", "wwwroot", "css", "output.css"), plan.OutputPath);
        Assert.Contains(plan.WatchRoots, root => string.Equals(root.FullPath, Path.Combine(workspace.RootPath, "Tailwind"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.WatchRoots, root => string.Equals(root.FullPath, Path.Combine(workspace.RootPath, "src", "SharedUi"), StringComparison.OrdinalIgnoreCase));
        Assert.True(TailwindWorkspaceDetector.TryExtractInputOutputPaths(plan.ScriptCommand, out var inputPath, out var outputPath));
        Assert.Equal("./input.css", inputPath);
        Assert.Equal("../src/SampleApp/wwwroot/css/output.css", outputPath);
    }

    [Fact]
    public void TailwindWorkspaceDetector_ReturnsNull_WhenNoTailwindWorkspaceExists()
    {
        using var workspace = new TailwindWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "SampleApp", "SampleApp.csproj"), "<Project />");
        workspace.WriteFile(
            "package.json",
            """
            {
              "name": "plain-app",
              "private": true,
              "scripts": {
                "build": "vite build"
              }
            }
            """);

        var projectPath = Path.Combine(workspace.RootPath, "src", "SampleApp", "SampleApp.csproj");
        var workingDirectory = Path.Combine(workspace.RootPath, "src", "SampleApp");

        var plan = TailwindWorkspaceDetector.TryDetect(projectPath, workingDirectory);

        Assert.Null(plan);
    }

    private sealed class TailwindWorkspace : IDisposable
    {
        public TailwindWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.Mcp.DotNetWatch.TailwindTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(Path.Combine(RootPath, ".git"));
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
        }
    }
}
