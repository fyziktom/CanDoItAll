using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowArtifactContentStorePortabilityTests
{
    [Fact]
    public async Task SaveContent_uses_workspace_atomic_mutation_and_round_trips_content()
    {
        using var workspace = new TemporaryDirectory();
        FileWorkflowArtifactContentStore store = CreateStore(workspace.Path);
        WorkflowArtifactRecord artifact = CreateArtifact("workflow-runs/run-1/payloads/result.json");

        await store.SaveContentAsync(artifact, "{\"result\":true}");
        WorkflowArtifactContent? content = await store.ReadContentAsync(artifact);

        Assert.NotNull(content);
        Assert.Equal("{\"result\":true}", content.Content);
    }

    [Fact]
    public async Task SaveContent_rejects_foreign_physical_syntax_at_logical_boundary()
    {
        using var workspace = new TemporaryDirectory();
        FileWorkflowArtifactContentStore store = CreateStore(workspace.Path);
        WorkflowArtifactRecord artifact = CreateArtifact(@"C:\outside\payload.json");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveContentAsync(artifact, "blocked"));

        Assert.Contains("canonical logical path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveContent_rejects_artifact_root_link_without_touching_outside_target()
    {
        using var workspace = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string artifactRoot = Path.Combine(workspace.Path, "artifacts");
        Directory.CreateDirectory(artifactRoot);
        string linkedRunRoot = Path.Combine(artifactRoot, "workflow-runs");
        try
        {
            Directory.CreateSymbolicLink(linkedRunRoot, outside.Path);
        }
        catch (Exception exception) when (
            OperatingSystem.IsWindows() &&
            exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        try
        {
            FileWorkflowArtifactContentStore store = CreateStore(workspace.Path);
            WorkflowArtifactRecord artifact = CreateArtifact("workflow-runs/run-1/payloads/result.json");

            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveContentAsync(artifact, "blocked"));

            Assert.Empty(Directory.EnumerateFileSystemEntries(outside.Path));
        }
        finally
        {
            Directory.Delete(linkedRunRoot);
        }
    }

    private static FileWorkflowArtifactContentStore CreateStore(string workspaceRoot)
    {
        var pathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        var scope = WorkspaceScopeDescriptor.Sandbox;
        return new FileWorkflowArtifactContentStore(
            scope,
            new WorkspacePathResolutionService(workspaceRoot, pathPolicyFactory, scope),
            new WorkspaceFileService(workspaceRoot, pathPolicyFactory, scope));
    }

    private static WorkflowArtifactRecord CreateArtifact(string storagePath)
        => new(
            WorkflowArtifactId.New(),
            WorkflowRunId.New(),
            WorkflowArtifactKind.Json,
            NodeId: null,
            "result.json",
            "application/json",
            storagePath,
            "Portability test artifact.",
            DateTimeOffset.UtcNow);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"CanDoItAll.WorkflowArtifactPortability.{Guid.NewGuid():N}");
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
}
