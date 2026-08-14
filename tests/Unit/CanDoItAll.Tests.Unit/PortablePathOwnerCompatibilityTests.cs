using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixPortabilityCore")]
public sealed class PortablePathOwnerCompatibilityTests
{
    [Fact]
    public void Configuration_path_owners_use_the_same_bounded_expansion_contract()
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DATA_ROOT"] = "${BASE_ROOT}/data",
            ["BASE_ROOT"] = "/srv/candoitall"
        };
        string? Resolve(string name) => variables.GetValueOrDefault(name);

        var workspaceResult = WorkspacePathPolicy.ExpandPortablePath(
            "${DATA_ROOT}/workspace",
            "/home/tester",
            Resolve,
            PortablePathTemplateCompatibility.Canonical);
        var mafResult = MafRuntimePathResolver.ExpandPortablePath(
            "${DATA_ROOT}/workspace",
            "/home/tester",
            Resolve,
            PortablePathTemplateCompatibility.Canonical);
        var controlPlaneResult = ControlPlanePathDefaults.ExpandConfiguredPath(
            "${DATA_ROOT}/workspace",
            "/home/tester",
            Resolve,
            PortablePathTemplateCompatibility.Canonical);

        Assert.Equal("/srv/candoitall/data/workspace", workspaceResult);
        Assert.Equal(workspaceResult, mafResult);
        Assert.Equal(workspaceResult, controlPlaneResult);
    }

    [Fact]
    public void Logical_path_owners_emit_the_same_native_and_canonical_forms()
    {
        const string legacyLogicalPath = @"managed-files\project-media\quote.pdf";

        var canonicalWorkspacePath = WorkspacePathPolicy.NormalizeRelativePath(legacyLogicalPath);
        var nativeStoragePath = FileSystemStoragePathPolicy.NormalizeRelativeKey(legacyLogicalPath);

        Assert.Equal("managed-files/project-media/quote.pdf", canonicalWorkspacePath);
        Assert.Equal(Path.Combine("managed-files", "project-media", "quote.pdf"), nativeStoragePath);
    }

    [Fact]
    public void Persisted_artifact_writer_is_independent_of_host_separators()
    {
        var store = new ManagedArtifactStore(null!);

        var relativePath = store.GetRelativePath(@"project-media\files", "quote.pdf");

        Assert.Equal("managed-files/project-media/files/quote.pdf", relativePath);
    }

    [Fact]
    public void Physical_path_classifier_distinguishes_path_categories_without_host_parsing()
    {
        Assert.Equal(PhysicalPathSyntax.Relative, PhysicalPathSyntaxPolicy.Classify("workspace/output.json"));
        Assert.Equal(PhysicalPathSyntax.UnixAbsolute, PhysicalPathSyntaxPolicy.Classify("/var/lib/candoitall"));
        Assert.Equal(PhysicalPathSyntax.WindowsDriveAbsolute, PhysicalPathSyntaxPolicy.Classify(@"C:\data\workspace"));
        Assert.Equal(PhysicalPathSyntax.WindowsDriveRelative, PhysicalPathSyntaxPolicy.Classify(@"C:workspace"));
        Assert.Equal(PhysicalPathSyntax.WindowsUnc, PhysicalPathSyntaxPolicy.Classify(@"\\server\share\workspace"));
        Assert.Equal(PhysicalPathSyntax.WindowsDevice, PhysicalPathSyntaxPolicy.Classify(@"\\?\C:\workspace"));
        Assert.Equal(PhysicalPathSyntax.Uri, PhysicalPathSyntaxPolicy.Classify("https://example.test/workspace"));
    }

    [Theory]
    [InlineData("windows", true, @"C:\Users\tester")]
    [InlineData("linux", false, "/home/tester")]
    [InlineData("macos", false, "/Users/tester")]
    public void Golden_host_matrix_keeps_physical_and_logical_path_semantics_distinct(
        string hostName,
        bool isWindowsHost,
        string homeDirectory)
    {
        Assert.True(PhysicalPathSyntaxPolicy.IsNativeOrRelative(PhysicalPathSyntax.Relative, isWindowsHost));
        Assert.Equal(
            isWindowsHost,
            PhysicalPathSyntaxPolicy.IsNativeOrRelative(PhysicalPathSyntax.WindowsDriveAbsolute, isWindowsHost));
        Assert.Equal(
            isWindowsHost,
            PhysicalPathSyntaxPolicy.IsNativeOrRelative(PhysicalPathSyntax.WindowsUnc, isWindowsHost));
        Assert.Equal(
            !isWindowsHost,
            PhysicalPathSyntaxPolicy.IsNativeOrRelative(PhysicalPathSyntax.UnixAbsolute, isWindowsHost));
        Assert.False(PhysicalPathSyntaxPolicy.IsNativeOrRelative(PhysicalPathSyntax.Uri, isWindowsHost));

        var expandedHome = PortablePathTemplate.Expand(
            "~/workspace",
            homeDirectory,
            static _ => null,
            PortablePathTemplateCompatibility.Canonical);
        var expandedUnicode = PortablePathTemplate.Expand(
            @"${DATA_ROOT}/résumé\2026",
            homeDirectory,
            name => name == "DATA_ROOT" ? expandedHome : null,
            PortablePathTemplateCompatibility.Canonical);
        var logicalPath = LogicalPath.Parse("résumé/2026/report.json");

        Assert.Equal(homeDirectory.TrimEnd('/', '\\') + "/workspace", expandedHome);
        Assert.Equal(expandedHome + @"/résumé\2026", expandedUnicode);
        Assert.Equal("résumé/2026/report.json", logicalPath.Value);
        Assert.Equal(logicalPath, LogicalPath.Parse(logicalPath.Value));
        Assert.False(string.IsNullOrWhiteSpace(hostName));
    }

    [Fact]
    public void Foreign_absolute_paths_fail_before_any_owner_can_reinterpret_them()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("foreign-path-owner-contract");
        var foreignPath = OperatingSystem.IsWindows()
            ? "/var/lib/candoitall/workspace"
            : @"C:\data\workspace";

        try
        {
            var guard = new WorkspacePathAccessGuard(
                new TestWorkspacePathResolver(workspaceRoot),
                TestWorkspaceServices.PhysicalPathPolicyFactory);
            var guardResult = guard.ResolveWorkspacePath(foreignPath);
            var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot);
            var policyResult = policy.TryResolveWorkspacePath(
                foreignPath,
                allowWorkspaceRoot: false,
                out _,
                out var policyMessage);
            var mafException = Assert.Throws<InvalidOperationException>(() =>
                MafRuntimePathResolver.ResolvePathFromWorkspace(
                    workspaceRoot,
                    foreignPath,
                    allowExternal: false,
                    physicalPathPolicyFactory: TestWorkspaceServices.PhysicalPathPolicyFactory));
            var controlPlaneException = Assert.Throws<InvalidOperationException>(() =>
                ControlPlanePathDefaults.ResolveConfiguredPath(workspaceRoot, foreignPath));

            Assert.False(guardResult.IsSuccess);
            Assert.Contains("host-bound", guardResult.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(policyResult);
            Assert.Contains("different host platform", policyMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("host-bound", mafException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("host-bound", controlPlaneException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Workspace_containment_rejects_foreign_candidate_and_owner_root_syntax()
    {
        var nativeRoot = TestFileSystem.CreateTemporaryRoot("foreign-containment-owner-contract");
        var foreignPath = OperatingSystem.IsWindows()
            ? "/var/lib/candoitall/workspace"
            : @"C:\data\workspace";

        try
        {
            var policy = TestWorkspaceServices.CreatePathPolicy(nativeRoot);
            Assert.Throws<WorkspacePathResolutionException>(() =>
                policy.IsPathWithinRoot(foreignPath, nativeRoot));
            Assert.Throws<WorkspacePathResolutionException>(() =>
                policy.IsPathWithinRoot(nativeRoot, foreignPath));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(nativeRoot);
        }
    }

    [Fact]
    public void Maf_workspace_guard_keeps_case_distinct_unix_roots_separate()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = TestFileSystem.CreateTemporaryRoot("maf-case-sensitive-root");
        var workspaceRoot = Path.Combine(parent, "Workspace");
        var siblingRoot = Path.Combine(parent, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(siblingRoot);

        try
        {
            var guard = new WorkspaceRuntimeFileAccessGuard(
                workspaceRoot,
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                WorkspaceScopeDescriptor.Sandbox,
                new AgentWorkspaceToolAccessSettings());

            Assert.True(guard.IsManagedWorkspaceAbsolutePath(Path.Combine(workspaceRoot, "file.txt")));
            Assert.False(guard.IsManagedWorkspaceAbsolutePath(Path.Combine(siblingRoot, "file.txt")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(parent);
        }
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
