using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRagRetrieverTests
{
    [Fact]
    public async Task Project_ambient_search_returns_only_current_project_media()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var foreignProjectId = Guid.Parse("be2ebfd7-7766-43f9-9b2e-8051d0b0d99d");
        var currentRoot = CreateProjectFilesRoot(workspaceRoot, projectId);
        var foreignRoot = CreateProjectFilesRoot(workspaceRoot, foreignProjectId);
        await File.WriteAllTextAsync(
            Path.Combine(currentRoot, "current.md"),
            "analyze project structure summary CURRENT-PROJECT-CONTEXT");
        await File.WriteAllTextAsync(
            Path.Combine(foreignRoot, "foreign.md"),
            "analyze project structure summary FOREIGN-PROJECT-CONTEXT");
        await File.WriteAllTextAsync(
            Path.Combine(workspaceRoot, "project-structure-context-brief.md"),
            "analyze project structure summary SHARED-ROOT-CONTEXT");

        try
        {
            var retriever = new WorkspaceRagRetriever(
                workspaceRoot,
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")));

            var searchRoots = retriever.ResolveSearchRoots(workspaceRoot);
            var results = (await retriever.SearchAsync(
                    searchRoots,
                    "analyze project structure summary",
                    maxResults: 5,
                    maxFilesToScan: 256,
                    minQueryTerms: 2,
                    minMatchedTerms: 2,
                    minScore: 2,
                    extensions: new HashSet<string>([".md"], StringComparer.OrdinalIgnoreCase),
                    excludedPaths: null,
                    CancellationToken.None))
                .ToArray();

            Assert.Equal(2, searchRoots.Count);
            Assert.Contains(
                searchRoots,
                root => string.Equals(
                    root,
                    Path.GetFullPath(currentRoot),
                    StringComparison.OrdinalIgnoreCase));
            Assert.Single(searchRoots, Directory.Exists);
            var result = Assert.Single(results);
            Assert.Contains("CURRENT-PROJECT-CONTEXT", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("FOREIGN-PROJECT-CONTEXT", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("SHARED-ROOT-CONTEXT", result.Text, StringComparison.Ordinal);
            Assert.Contains(projectId.ToString("N"), result.SourceName, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Project_ambient_search_keeps_canonical_roots_without_shared_fallback_when_media_is_missing()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(workspaceRoot, "project-structure-context-brief.md"),
                "stale shared context");
            var retriever = new WorkspaceRagRetriever(
                workspaceRoot,
                WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));

            var searchRoots = retriever.ResolveSearchRoots(workspaceRoot);
            var results = await retriever.SearchAsync(
                searchRoots,
                "stale shared context",
                maxResults: 5,
                maxFilesToScan: 256,
                minQueryTerms: 2,
                minMatchedTerms: 2,
                minScore: 2,
                extensions: new HashSet<string>([".md"], StringComparer.OrdinalIgnoreCase),
                excludedPaths: null,
                CancellationToken.None);

            Assert.Equal(2, searchRoots.Count);
            Assert.All(searchRoots, root =>
            {
                Assert.Contains("managed-files", root, StringComparison.OrdinalIgnoreCase);
                Assert.False(Directory.Exists(root));
            });
            Assert.Empty(results);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void Explicit_rag_root_and_non_project_ambient_root_preserve_existing_behavior()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var explicitRoot = Path.Combine(workspaceRoot, "explicit-source");
        Directory.CreateDirectory(explicitRoot);

        try
        {
            var projectRetriever = new WorkspaceRagRetriever(
                workspaceRoot,
                WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));
            var sandboxRetriever = new WorkspaceRagRetriever(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox);

            Assert.Equal(
                Path.GetFullPath(explicitRoot),
                Assert.Single(projectRetriever.ResolveSearchRoots(explicitRoot)));
            Assert.Equal(
                Path.GetFullPath(workspaceRoot),
                Assert.Single(sandboxRetriever.ResolveSearchRoots(workspaceRoot)));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void Project_explicit_rag_root_cannot_select_foreign_or_shared_project_media()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var foreignRoot = CreateProjectFilesRoot(
            workspaceRoot,
            Guid.Parse("be2ebfd7-7766-43f9-9b2e-8051d0b0d99d"));
        var sharedMediaRoot = Path.Combine(
            workspaceRoot,
            "managed-files",
            "project-media",
            "files");

        try
        {
            var retriever = new WorkspaceRagRetriever(
                workspaceRoot,
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")));

            Assert.Empty(retriever.ResolveSearchRoots(foreignRoot));
            Assert.Empty(retriever.ResolveSearchRoots(sharedMediaRoot));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(WorkspaceRagRetrieverTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateProjectFilesRoot(
        string workspaceRoot,
        Guid projectId)
    {
        var root = Path.Combine(
            workspaceRoot,
            "managed-files",
            "project-media",
            "files",
            projectId.ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
