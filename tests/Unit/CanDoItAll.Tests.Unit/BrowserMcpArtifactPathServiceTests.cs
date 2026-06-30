using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class BrowserMcpArtifactPathServiceTests
{
    [Fact]
    public void EnsureWritableArtifactDirectories_creates_unscoped_and_scoped_artifact_directories()
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
            Assert.True(Directory.Exists(Path.Combine(workspaceRoot, "artifacts", "scopes", "organization", "org-1", "process-runs", "run-1", "browser")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void TryMirrorToScopedArtifactPath_copies_unscoped_browser_artifact_to_current_scope()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("browser-mcp-artifacts");
        try
        {
            var scope = WorkspaceScopeDescriptor.Organization("Org 1");
            var fileName = "artifacts/process-runs/run-1/browser/desktop.png";
            var unscopedFullPath = Path.Combine(workspaceRoot, "artifacts", "process-runs", "run-1", "browser", "desktop.png");
            Directory.CreateDirectory(Path.GetDirectoryName(unscopedFullPath)!);
            File.WriteAllText(unscopedFullPath, "screenshot-bytes");

            var mirrored = BrowserMcpArtifactPathService.TryMirrorToScopedArtifactPath(
                workspaceRoot,
                scope,
                fileName,
                out var scopedRelativePath);

            Assert.True(mirrored);
            Assert.Equal("artifacts/scopes/organization/org-1/process-runs/run-1/browser/desktop.png", scopedRelativePath);
            Assert.Equal(
                "screenshot-bytes",
                File.ReadAllText(Path.Combine(workspaceRoot, "artifacts", "scopes", "organization", "org-1", "process-runs", "run-1", "browser", "desktop.png")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
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
            File.WriteAllText(sourceFullPath, "screenshot-bytes");

            var result = BrowserMcpArtifactPathService.TryImportAfterInvocation(
                workspaceRoot,
                scope,
                fileName,
                "run-1");

            Assert.True(result.Imported);
            Assert.Contains("artifacts/process-runs/run-1/browser/screenshot.png", result.ImportedRelativePaths);
            Assert.Contains("artifacts/scopes/organization/org-1/process-runs/run-1/browser/screenshot.png", result.ImportedRelativePaths);
            Assert.Equal(
                "screenshot-bytes",
                File.ReadAllText(Path.Combine(workspaceRoot, "artifacts", "process-runs", "run-1", "browser", "screenshot.png")));
            Assert.Equal(
                "screenshot-bytes",
                File.ReadAllText(Path.Combine(workspaceRoot, "artifacts", "scopes", "organization", "org-1", "process-runs", "run-1", "browser", "screenshot.png")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
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
}
