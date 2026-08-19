using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceExecutableLocatorTests
{
    [Fact]
    public void Windows_resolution_uses_Pathext_order()
    {
        using var directory = new TemporaryDirectory();
        var cmdPath = Path.Combine(directory.Path, "portable-tool.CMD");
        var exePath = Path.Combine(directory.Path, "portable-tool.EXE");
        File.WriteAllText(cmdPath, string.Empty);
        File.WriteAllText(exePath, string.Empty);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = directory.Path,
            ["PATHEXT"] = ".CMD;.EXE"
        };
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatform.Windows,
            name => environment.GetValueOrDefault(name));

        var resolved = locator.ResolveExecutablePath(["portable-tool"]);

        Assert.Equal(Path.GetFullPath(cmdPath), resolved);
    }

    [Fact]
    public void Unix_resolution_does_not_probe_Windows_extensions()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "portable-tool.exe"), string.Empty);
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatform.Linux,
            name => name == "PATH" ? directory.Path : null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath(["portable-tool"]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.Missing, exception.Failure);
    }

    [Fact]
    public void MacOS_contract_uses_exact_executable_names_only()
    {
        var candidates = WorkspaceExecutableLocator.GetCandidateFileNames(
            "portable-tool",
            LocalHostPlatform.MacOS,
            ".COM;.EXE;.BAT;.CMD");

        Assert.Equal(["portable-tool"], candidates);
    }

    [Theory]
    [InlineData(".EXE;.C/MD")]
    [InlineData(".EXE;C:\\CMD")]
    [InlineData(".EXE;https:.CMD")]
    [InlineData(".EXE;\t.CMD")]
    [InlineData(".EXE;.exe")]
    [InlineData("   ")]
    public void Windows_Pathext_rejects_malformed_path_like_control_duplicate_or_whitespace_entries(
        string pathExtensions)
    {
        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            WorkspaceExecutableLocator.GetCandidateFileNames(
                "portable-tool",
                LocalHostPlatform.Windows,
                pathExtensions));

        Assert.Equal(WorkspaceExecutableResolutionFailure.InvalidCandidate, exception.Failure);
    }

    [Fact]
    public void Windows_Pathext_rejects_excessive_entry_count()
    {
        string pathExtensions = string.Join(';', Enumerable.Range(0, 33).Select(index => $".X{index}"));

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            WorkspaceExecutableLocator.GetCandidateFileNames(
                "portable-tool",
                LocalHostPlatform.Windows,
                pathExtensions));

        Assert.Equal(WorkspaceExecutableResolutionFailure.InvalidCandidate, exception.Failure);
        Assert.Contains("1 through 32", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("portable\ttool")]
    [InlineData("portable\u007ftool")]
    public void Resolution_rejects_all_candidate_control_characters(string candidate)
    {
        var locator = new WorkspaceExecutableLocator(LocalHostPlatform.Linux, _ => null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath([candidate]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.InvalidCandidate, exception.Failure);
    }

    [Fact]
    public void Resolution_rejects_excessive_candidate_length()
    {
        var locator = new WorkspaceExecutableLocator(LocalHostPlatform.Linux, _ => null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath([new string('x', 1025)]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.InvalidCandidate, exception.Failure);
    }

    [Fact]
    public void Executable_authorization_uses_Windows_suffix_and_case_semantics()
    {
        var policy = new WorkspaceExecutableAuthorizationPolicy(
            LocalHostPlatform.Windows,
            ".CMD;.EXE");

        Assert.True(policy.IsAllowedResolvedPath(
            @"C:\runtime\DOTNET.EXE",
            ["dotnet"]));
        Assert.True(policy.IsAllowedResolvedPath(
            @"C:\runtime\npx.CMD",
            ["npx"]));
    }

    [Fact]
    public void Executable_authorization_keeps_Unix_names_case_and_suffix_exact()
    {
        var policy = new WorkspaceExecutableAuthorizationPolicy(
            LocalHostPlatform.Linux,
            ".CMD;.EXE");

        Assert.True(policy.IsAllowedResolvedPath("/usr/bin/dotnet", ["dotnet"]));
        Assert.False(policy.IsAllowedResolvedPath("/usr/bin/DOTNET", ["dotnet"]));
        Assert.False(policy.IsAllowedResolvedPath("/usr/bin/dotnet.exe", ["dotnet"]));
    }

    [Fact]
    public void Resolution_uses_PATH_directory_order_when_multiple_candidates_exist()
    {
        using var firstDirectory = new TemporaryDirectory();
        using var secondDirectory = new TemporaryDirectory();
        var executableName = OperatingSystem.IsWindows()
            ? "portable-tool.EXE"
            : "portable-tool";
        var firstPath = Path.Combine(firstDirectory.Path, executableName);
        var secondPath = Path.Combine(secondDirectory.Path, executableName);
        File.WriteAllText(firstPath, OperatingSystem.IsWindows() ? string.Empty : "#!/bin/sh\nexit 0\n");
        File.WriteAllText(secondPath, OperatingSystem.IsWindows() ? string.Empty : "#!/bin/sh\nexit 0\n");
        MakeExecutable(firstPath);
        MakeExecutable(secondPath);
        var path = string.Join(Path.PathSeparator, firstDirectory.Path, secondDirectory.Path);
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatformExtensions.CaptureCurrent(),
            name => name switch
            {
                "PATH" => path,
                "PATHEXT" => ".EXE",
                _ => null
            });

        var resolved = locator.ResolveExecutablePath(["portable-tool"]);

        Assert.Equal(ResolveExpectedCanonicalPath(firstPath), resolved);
    }

    [Fact]
    public void Resolution_reports_typed_missing_failure()
    {
        using var directory = new TemporaryDirectory();
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatformExtensions.CaptureCurrent(),
            name => name == "PATH" ? directory.Path : null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath(["portable-tool-that-does-not-exist"]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.Missing, exception.Failure);
    }

    [Fact]
    public void Unix_resolution_rejects_foreign_Windows_explicit_path()
    {
        var locator = new WorkspaceExecutableLocator(LocalHostPlatform.Linux, _ => null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath([@"C:\tools\portable-tool.exe"]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.ForeignPathSyntax, exception.Failure);
    }

    [Fact]
    public void Unix_actual_host_requires_execute_permission_and_resolves_final_symlink_target()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var targetPath = Path.Combine(directory.Path, "portable-tool-target");
        var linkPath = Path.Combine(directory.Path, "portable-tool");
        File.WriteAllText(targetPath, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(targetPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.CreateSymbolicLink(linkPath, targetPath);
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatform.Linux,
            name => name == "PATH" ? directory.Path : null);

        var notExecutable = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath(["portable-tool"]));
        Assert.Equal(WorkspaceExecutableResolutionFailure.NotExecutable, notExecutable.Failure);

        File.SetUnixFileMode(
            targetPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Assert.Equal(ResolveExpectedCanonicalPath(targetPath), locator.ResolveExecutablePath(["portable-tool"]));
    }

    [Fact]
    public void Unix_actual_host_requires_execute_access_for_the_current_file_owner_class()
    {
        if (OperatingSystem.IsWindows() || string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var executablePath = Path.Combine(directory.Path, "other-class-only-tool");
        File.WriteAllText(executablePath, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(
            executablePath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherExecute);
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatform.Linux,
            name => name == "PATH" ? directory.Path : null);

        var exception = Assert.Throws<WorkspaceExecutableResolutionException>(() =>
            locator.ResolveExecutablePath(["other-class-only-tool"]));

        Assert.Equal(WorkspaceExecutableResolutionFailure.NotExecutable, exception.Failure);
    }

    [Fact]
    public void Unix_actual_host_resolves_intermediate_directory_links_to_the_canonical_path()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        string targetDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "target")).FullName;
        string linkDirectory = Path.Combine(directory.Path, "link");
        Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
        string targetPath = Path.Combine(targetDirectory, "portable-tool");
        File.WriteAllText(targetPath, "#!/bin/sh\nexit 0\n");
        MakeExecutable(targetPath);
        var locator = new WorkspaceExecutableLocator(
            LocalHostPlatform.Linux,
            name => name == "PATH" ? linkDirectory : null);

        string resolved = locator.ResolveExecutablePath(["portable-tool"]);

        Assert.Equal(ResolveExpectedCanonicalPath(targetPath), resolved);
    }

    [Fact]
    public void Actual_host_authorizes_the_final_symlink_target_not_the_link_name()
    {
        using var directory = new TemporaryDirectory();
        var platform = LocalHostPlatformExtensions.CaptureCurrent();
        var approvedName = OperatingSystem.IsWindows()
            ? "approved-tool.exe"
            : "approved-tool";
        var targetName = OperatingSystem.IsWindows()
            ? "unapproved-target.exe"
            : "unapproved-target";
        var targetPath = Path.Combine(directory.Path, targetName);
        var linkPath = Path.Combine(directory.Path, approvedName);
        File.WriteAllText(
            targetPath,
            OperatingSystem.IsWindows() ? string.Empty : "#!/bin/sh\nexit 0\n");
        MakeExecutable(targetPath);
        File.CreateSymbolicLink(linkPath, targetPath);
        var locator = new WorkspaceExecutableLocator(
            platform,
            name => name switch
            {
                "PATH" => directory.Path,
                "PATHEXT" => ".EXE",
                _ => null
            });

        var resolved = locator.ResolveExecutablePath(["approved-tool"]);
        var policy = new WorkspaceExecutableAuthorizationPolicy(platform, ".EXE");

        Assert.Equal(ResolveExpectedCanonicalPath(targetPath), resolved);
        Assert.False(policy.IsAllowedResolvedPath(resolved, ["approved-tool"]));
    }

    private static string ResolveExpectedCanonicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsMacOS() && fullPath.StartsWith("/var/", StringComparison.Ordinal)
            ? $"/private{fullPath}"
            : fullPath;
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"CanDoItAll.WorkspaceExecutableLocatorTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
