using CanDoItAll.Infrastructure;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Storage;

[Trait("Category", "UnixPortabilityCore")]
[Trait("Category", "UnixRuntimePortability")]
public sealed class ApplicationStoragePortabilityContractTests
{
    [Theory]
    [InlineData(HostPlatformFamily.Windows, "C:\\Users\\operator\\AppData\\Local", "C:\\Users\\operator", "C:\\Temp", "C:\\Users\\operator\\AppData\\Local\\CanDoItAll\\workspace")]
    [InlineData(HostPlatformFamily.Linux, "/home/operator/.local/share", "/home/operator", "/tmp", "/home/operator/.local/share/candoitall/workspace")]
    [InlineData(HostPlatformFamily.MacOS, "/Users/operator/Library/Application Support", "/Users/operator", "/private/tmp", "/Users/operator/Library/Application Support/CanDoItAll/workspace")]
    public void Purpose_root_matrix_keeps_workspace_outside_the_repository(
        HostPlatformFamily platform,
        string localApplicationData,
        string homeDirectory,
        string temporaryRoot,
        string expectedWorkspaceRoot)
    {
        var environment = new ApplicationRootEnvironment(
            platform,
            homeDirectory,
            localApplicationData,
            temporaryRoot,
            new Dictionary<string, string?>(StringComparer.Ordinal));

        ApplicationPurposeRoots roots = ApplicationPurposeRootPolicy.Resolve(environment);

        Assert.Equal(expectedWorkspaceRoot, roots.WorkspaceRoot);
        Assert.NotEqual(roots.WorkspaceRoot, roots.ControlPlaneRoot);
        Assert.NotEqual(roots.StateRoot, roots.LogsRoot);
        Assert.NotEqual(roots.RuntimeTemporaryRoot, roots.ControlPlaneRoot);
    }

    [Theory]
    [InlineData("/var/folders/xy/session/T/", "/private/var/folders/xy/session/T/CanDoItAll/runtime")]
    [InlineData("/tmp/", "/private/tmp/CanDoItAll/runtime")]
    [InlineData("/private/var/folders/xy/session/T/", "/private/var/folders/xy/session/T/CanDoItAll/runtime")]
    [InlineData("/Users/operator/custom-temp/", "/Users/operator/custom-temp/CanDoItAll/runtime")]
    public void MacOS_runtime_temporary_root_uses_physical_system_aliases(
        string temporaryRoot,
        string expectedRuntimeTemporaryRoot)
    {
        var environment = new ApplicationRootEnvironment(
            HostPlatformFamily.MacOS,
            "/Users/operator",
            "/Users/operator/Library/Application Support",
            temporaryRoot,
            new Dictionary<string, string?>(StringComparer.Ordinal));

        ApplicationPurposeRoots roots = ApplicationPurposeRootPolicy.Resolve(environment);

        Assert.Equal(expectedRuntimeTemporaryRoot, roots.RuntimeTemporaryRoot);
    }

    [Fact]
    public void Linux_service_account_can_use_explicit_xdg_roots_without_a_home_directory()
    {
        var environment = new ApplicationRootEnvironment(
            HostPlatformFamily.Linux,
            string.Empty,
            string.Empty,
            "/service/tmp",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["XDG_CONFIG_HOME"] = "/service/config",
                ["XDG_DATA_HOME"] = "/service/data",
                ["XDG_STATE_HOME"] = "/service/state",
                ["XDG_RUNTIME_DIR"] = "/service/runtime"
            });

        ApplicationPurposeRoots roots = ApplicationPurposeRootPolicy.Resolve(environment);

        Assert.Equal("/service/data/candoitall/workspace", roots.WorkspaceRoot);
        Assert.Equal("/service/config/candoitall/control-plane", roots.ControlPlaneRoot);
        Assert.Equal("/service/state/candoitall", roots.StateRoot);
        Assert.Equal("/service/runtime/candoitall", roots.RuntimeTemporaryRoot);
    }

    [Fact]
    public void Legacy_foreign_absolute_path_requires_rebind_without_native_normalization()
    {
        HostPathContext linuxHost = HostPathContext.CreateForTest(
            HostPlatformFamily.Linux,
            "linux-host-binding");

        HostBoundPathRecord imported = HostBoundPathPolicy.ImportLegacy(
            @"C:\legacy\workspace",
            linuxHost);

        Assert.Equal(HostBoundPathState.NeedsRebind, imported.State);
        Assert.Equal(HostPlatformFamily.Windows, imported.PlatformFamily);
        Assert.Equal(PhysicalPathSyntax.WindowsDriveAbsolute, imported.PathSyntax);
        Assert.Equal(@"C:\legacy\workspace", imported.Path);
        Assert.False(HostBoundPathPolicy.TryResolve(imported, linuxHost, out _, out _));
    }

    [Fact]
    public void Current_binding_is_host_specific_and_rejects_a_different_host_on_the_same_platform()
    {
        HostPathContext sourceHost = HostPathContext.CreateForTest(
            HostPlatformFamily.Linux,
            "source-host-binding");
        HostPathContext destinationHost = HostPathContext.CreateForTest(
            HostPlatformFamily.Linux,
            "destination-host-binding");
        HostBoundPathRecord record = HostBoundPathPolicy.Bind(
            "/srv/candoitall/workspace",
            sourceHost,
            DateTimeOffset.Parse("2026-08-09T12:00:00Z"));

        bool resolved = HostBoundPathPolicy.TryResolve(
            record,
            destinationHost,
            out _,
            out string diagnostic);

        Assert.False(resolved);
        Assert.Contains("rebind", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/srv/candoitall/workspace", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_same_platform_legacy_path_still_requires_explicit_rebind()
    {
        string root = Path.Combine(Path.GetTempPath(), $"host-bound-legacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            HostPathContext currentHost = HostPathContext.CaptureCurrent();

            HostBoundPathRecord imported = HostBoundPathPolicy.ImportLegacy(root, currentHost);

            Assert.Equal(HostBoundPathState.NeedsRebind, imported.State);
            Assert.Empty(imported.HostBindingId);
            Assert.False(HostBoundPathPolicy.TryResolve(imported, currentHost, out _, out string diagnostic));
            Assert.Contains("rebind", diagnostic, StringComparison.OrdinalIgnoreCase);

            HostBoundPathRecord rebound = HostBoundPathPolicy.RebindCurrent(root, DateTimeOffset.UtcNow);
            Assert.True(HostBoundPathPolicy.TryResolve(rebound, currentHost, out string resolved, out _));
            Assert.Equal(Path.GetFullPath(root), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Current_host_purpose_roots_are_owned_writable_and_outside_the_repository()
    {
        ApplicationPurposeRoots roots = ApplicationPurposeRootPolicy.ResolveCurrent();
        string repositoryRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        string[] purposeRoots =
        [
            roots.WorkspaceRoot,
            roots.ControlPlaneRoot,
            roots.DataProtectionKeysRoot,
            roots.StateRoot,
            roots.LogsRoot,
            roots.RuntimeTemporaryRoot
        ];

        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        foreach (string root in purposeRoots.Distinct(pathComparer))
        {
            Assert.True(Path.IsPathFullyQualified(root));
            Assert.Contains("candoitall", root, StringComparison.OrdinalIgnoreCase);
            string relativeToRepository = Path.GetRelativePath(repositoryRoot, root);
            Assert.True(
                Path.IsPathFullyQualified(relativeToRepository) ||
                relativeToRepository == ".." ||
                relativeToRepository.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal),
                $"Purpose root '{root}' must remain outside repository '{repositoryRoot}'.");

            string probeRoot = Path.Combine(root, $".purpose-root-probe-{Guid.NewGuid():N}");
            string probePath = Path.Combine(probeRoot, "write-proof.txt");
            try
            {
                Directory.CreateDirectory(probeRoot);
                File.WriteAllText(probePath, "portable");
                Assert.Equal("portable", File.ReadAllText(probePath));
                if (!OperatingSystem.IsWindows())
                {
                    UnixFileMode directoryMode = File.GetUnixFileMode(probeRoot);
                    UnixFileMode fileMode = File.GetUnixFileMode(probePath);
                    Assert.Equal(
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                        directoryMode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
                    Assert.Equal(
                        UnixFileMode.UserRead | UnixFileMode.UserWrite,
                        fileMode & (UnixFileMode.UserRead | UnixFileMode.UserWrite));
                }
            }
            finally
            {
                if (Directory.Exists(probeRoot))
                {
                    Directory.Delete(probeRoot, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void Repository_path_binding_is_unresolved_after_cross_platform_import()
    {
        HostPathContext windowsHost = HostPathContext.CreateForTest(
            HostPlatformFamily.Windows,
            "windows-repository-host");
        HostPathContext linuxHost = HostPathContext.CreateForTest(
            HostPlatformFamily.Linux,
            "linux-repository-host");
        HostBoundPathRecord repositoryPath = HostBoundPathPolicy.Bind(
            @"C:\repositories\product",
            windowsHost,
            DateTimeOffset.Parse("2026-08-09T12:00:00Z"));

        bool resolved = HostBoundPathPolicy.TryResolve(
            repositoryPath,
            linuxHost,
            out _,
            out string diagnostic);

        Assert.False(resolved);
        Assert.Equal(HostBoundPathState.Active, repositoryPath.State);
        Assert.Equal(HostPlatformFamily.Windows, repositoryPath.PlatformFamily);
        Assert.Contains("rebind", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repositoryPath.Path, diagnostic, StringComparison.Ordinal);
    }
}
