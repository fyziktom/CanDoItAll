using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureSourceWorkspacePathResolverTests
{
    [Theory]
    [InlineData(WorkspaceScopeDescriptor.ArtifactManagedRootName)]
    [InlineData(WorkspaceScopeDescriptor.OutputManagedRootName)]
    [InlineData(WorkspaceScopeDescriptor.DataManagedRootName)]
    [InlineData(WorkspaceScopeDescriptor.IntegrationMapManagedRootName)]
    public void ResolveExistingFile_rejects_target_project_paths_that_escape_a_managed_root(
        string managedRootName)
    {
        using var temp = new ProjectStructureSourceWorkspaceTempDirectory();
        var escapedFile = Path.Combine(temp.Path, "escaped.xlsx");
        File.WriteAllText(escapedFile, "must not be imported through a traversal path");
        var projectId = Guid.NewGuid();
        var projectScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var externalTargets = TestExternalTargetPathRegistry.Create();
        var resolver = new ProjectStructureSourceWorkspacePathResolver(
            TestWorkspaceServices.CreatePathResolutionService(
                temp.Path,
                WorkspaceScopeDescriptor.Organization("active-organization"),
                externalTargets),
            new StaticWorkspacePathResolver(temp.Path),
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            externalTargets);
        var scopedRoot = ResolveManagedRoot(projectScope, managedRootName);
        var traversalPath = $"{scopedRoot}/../../../../escaped.xlsx";

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            resolver.ResolveExistingFile(projectId, traversalPath));

        Assert.Equal("SourceWorkspacePathInvalid", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("canonical", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'..'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveExistingFile_maps_typed_workspace_path_failure_to_agent_visible_input()
    {
        using var temp = new ProjectStructureSourceWorkspaceTempDirectory();
        var externalTargets = TestExternalTargetPathRegistry.Create();
        var resolver = new ProjectStructureSourceWorkspacePathResolver(
            TestWorkspaceServices.CreatePathResolutionService(
                temp.Path,
                externalTargetRegistry: externalTargets),
            new StaticWorkspacePathResolver(temp.Path),
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            externalTargets);
        var outsidePath = Path.Combine(
            Path.GetTempPath(),
            $"outside-{Guid.NewGuid():N}.xlsx");

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            resolver.ResolveExistingFile(Guid.NewGuid(), outsidePath));

        Assert.Equal("SourceWorkspacePathInvalid", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UnexpectedPathResolverFailureKind.InvalidOperation)]
    [InlineData(UnexpectedPathResolverFailureKind.Io)]
    public void ResolveExistingFile_leaves_untyped_path_resolver_failures_opaque(
        UnexpectedPathResolverFailureKind failureKind)
    {
        using var temp = new ProjectStructureSourceWorkspaceTempDirectory();
        Exception expected = failureKind switch
        {
            UnexpectedPathResolverFailureKind.InvalidOperation =>
                new InvalidOperationException($"Unexpected operation failure at '{temp.Path}'."),
            UnexpectedPathResolverFailureKind.Io =>
                new IOException($"Unexpected I/O failure at '{temp.Path}'."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unknown unexpected path resolver failure kind.")
        };
        var resolver = new ProjectStructureSourceWorkspacePathResolver(
            new ThrowingWorkspacePathResolutionService(expected),
            new StaticWorkspacePathResolver(temp.Path),
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            TestExternalTargetPathRegistry.Create());

        var exception = Record.Exception(() =>
            resolver.ResolveExistingFile(Guid.NewGuid(), "source.xlsx"));

        Assert.Same(expected, exception);
    }

    private static string ResolveManagedRoot(
        WorkspaceScopeDescriptor scope,
        string managedRootName)
    {
        return managedRootName switch
        {
            WorkspaceScopeDescriptor.ArtifactManagedRootName => scope.ArtifactRootRelativePath,
            WorkspaceScopeDescriptor.OutputManagedRootName => scope.OutputRootRelativePath,
            WorkspaceScopeDescriptor.DataManagedRootName => scope.DataRootRelativePath,
            WorkspaceScopeDescriptor.IntegrationMapManagedRootName => scope.IntegrationMapRootRelativePath,
            _ => throw new ArgumentOutOfRangeException(nameof(managedRootName), managedRootName, null)
        };
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => workspaceRoot;

        public string ResolveExportsRoot() => workspaceRoot;

        public string ResolveEvidenceRoot() => workspaceRoot;

        public string ResolveManagerArtifactsRoot() => workspaceRoot;
    }

    private sealed class ThrowingWorkspacePathResolutionService(Exception exception)
        : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => throw exception;

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => throw exception;
    }

    public enum UnexpectedPathResolverFailureKind
    {
        InvalidOperation,
        Io
    }
}

internal sealed class ProjectStructureSourceWorkspaceTempDirectory : IDisposable
{
    public ProjectStructureSourceWorkspaceTempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            nameof(ProjectStructureSourceWorkspaceTempDirectory),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
