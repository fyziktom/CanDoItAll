using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class BrowserMcpArtifactPathServiceTests
{
    [Theory]
    [InlineData("D")]
    [InlineData("N")]
    public void Imported_process_artifact_reference_resolves_from_the_project_tool_scope_without_granting_other_scopes(string idFormat) {
        var root = TestFileSystem.CreateTemporaryRoot("browser-artifact-reference");
        try {
            var processId = Guid.NewGuid().ToString(idFormat);
            var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
            var executionScope = WorkspaceScopeDescriptor.Organization("artifact-owner");
            var now = DateTimeOffset.UtcNow;
            var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Browser proof", "process-step", "qa",
                "proof", "proof", "unit-test", "system", "{}", "Capture", "", "fixture", "fixture",
                ExecutionState.Running, null, now, now, now, null, "", null, [], ProcessRunId: processId);
            using var audit = WorkspaceExecutionAuditContext.BeginScope(run, projectScope, executionScope);
            const string requested = ".playwright-mcp/current.png";
            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(root, executionScope, requested);
            var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(root, executionScope, requested);
            File.WriteAllBytes(Path.Combine(root, requested), [1, 2, 3]);
            var imported = BrowserMcpArtifactPathService.TryImportAfterInvocation(root, executionScope, requested, processId, before);
            var reference = Assert.Single(imported.ToolReferences);
            var stored = Assert.Single(imported.ImportedRelativePaths);
            Assert.StartsWith($"artifacts/process-runs/{Guid.Parse(processId):D}/browser/", reference, StringComparison.Ordinal);
            var result = BrowserMcpResultProjector.Project(ToolContractCatalog.BrowserTakeScreenshot, requested,
                JsonSerializer.SerializeToElement(new { isError = false, content = Array.Empty<object>() }), imported);
            Assert.Contains(reference, result.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(stored, result.Message, StringComparison.Ordinal);
            Assert.Equal(reference, Assert.Single(result.Artifacts));
            Assert.DoesNotContain(stored, JsonSerializer.Serialize(result), StringComparison.Ordinal);
            var policy = TestWorkspaceServices.CreatePathPolicy(root, projectScope);
            Assert.True(policy.TryResolveWorkspacePath(reference, false, out var resolved, out var failure), failure);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, stored.Replace('/', Path.DirectorySeparatorChar))), resolved.FullPath);
            Assert.False(policy.TryResolveWorkspacePath(stored, false, out _, out _));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(resolved.FullPath));
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void EnsureWritableArtifactDirectories_creates_requested_artifact_directory()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try
        {
            var scope = WorkspaceScopeDescriptor.Organization("Org 1");

            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(
                workspaceRoot,
                scope,
                "artifacts/process-runs/run-1/browser/desktop.png");

            Assert.True(Directory.Exists(Path.Combine(workspaceRoot, "artifacts", "process-runs", "run-1", "browser")));
            Assert.False(Directory.Exists(Path.Combine(workspaceRoot, "artifacts", "scopes", "organization", "org-1", "process-runs", "run-1", "browser")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Fresh_artifact_is_imported_to_immutable_current_scope_evidence() {
        var root = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try {
            var scope = WorkspaceScopeDescriptor.Organization("Org 1");
            const string fileName = "artifacts/process-runs/run-1/browser/desktop.png";
            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(root, scope, fileName);
            var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(root, scope, fileName);
            File.WriteAllText(Path.Combine(root, fileName), "first screenshot bytes");
            var first = BrowserMcpArtifactPathService.TryImportAfterInvocation(root, scope, fileName, "run-1", before);
            var firstPath = Assert.Single(first.ImportedRelativePaths);
            Assert.StartsWith(scope.ArtifactRootRelativePath + "/process-runs/run-1/browser/", firstPath);
            var next = BrowserMcpArtifactPathService.ObserveBeforeInvocation(root, scope, fileName);
            File.WriteAllText(Path.Combine(root, fileName), "second screenshot bytes");
            var second = BrowserMcpArtifactPathService.TryImportAfterInvocation(root, scope, fileName, "run-1", next);
            Assert.NotEqual(firstPath, Assert.Single(second.ImportedRelativePaths));
            Assert.Equal("first screenshot bytes", File.ReadAllText(Path.Combine(root, firstPath)));
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void EnsureWritableArtifactDirectories_creates_playwright_mcp_artifact_directory()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try
        {
            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization("Org 1"),
                ".playwright-mcp/screenshots/desktop.png");

            Assert.True(Directory.Exists(Path.Combine(workspaceRoot, ".playwright-mcp", "screenshots")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void TryImportAfterInvocation_copies_playwright_mcp_artifact_to_process_run_browser_artifacts()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try
        {
            var scope = WorkspaceScopeDescriptor.Organization("Org 1");
            var fileName = ".playwright-mcp/screenshot.png";
            var sourceFullPath = Path.Combine(workspaceRoot, ".playwright-mcp", "screenshot.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFullPath)!);
            var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(workspaceRoot, scope, fileName);
            File.WriteAllText(sourceFullPath, "screenshot-bytes");

            var result = BrowserMcpArtifactPathService.TryImportAfterInvocation(
                workspaceRoot,
                scope,
                fileName,
                "run-1", before);

            var savedPath = Assert.Single(result.ImportedRelativePaths);
            Assert.StartsWith("artifacts/scopes/organization/org-1/process-runs/run-1/browser/", savedPath);
            Assert.Equal("screenshot-bytes", File.ReadAllText(Path.Combine(workspaceRoot, savedPath)));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void ResolveArtifactScope_uses_the_owning_execution_scope()
    {
        var processRunId = Guid.NewGuid().ToString("D");
        var contextScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var executionScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var run = CreateRun(processRunId);

        using (WorkspaceExecutionAuditContext.BeginScope(
                   run,
                   contextWorkspaceScope: contextScope,
                   executionWorkspaceScope: executionScope))
        {
            Assert.Equal(
                executionScope,
                BrowserMcpArtifactPathService.ResolveArtifactScope(contextScope));
        }
    }

    [Fact]
    public void EnsureWritableArtifactDirectories_ignores_paths_that_escape_workspace_artifacts()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try
        {
            BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization("Org 1"),
                "artifacts/../outside/desktop.png");

            Assert.False(Directory.Exists(Path.Combine(workspaceRoot, "outside")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ExecutionRunRecord CreateRun(string processRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Browser artifact scope",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: processRunId,
            CausationId: Guid.NewGuid().ToString("D"),
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Capture browser evidence.",
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "gpt-5",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId,
            ProcessStepId: Guid.NewGuid().ToString("D"));
    }
}
