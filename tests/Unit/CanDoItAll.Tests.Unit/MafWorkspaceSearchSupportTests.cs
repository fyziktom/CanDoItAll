using CanDoItAll.AgentFramework.Maf;

namespace CanDoItAll.Tests.Unit;

public sealed class MafWorkspaceSearchSupportTests
{
    [Fact]
    public void EnumerateSearchFiles_keeps_accessible_RAG_context_when_a_sibling_directory_is_denied()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.MafWorkspaceSearchSupportTests",
            Guid.NewGuid().ToString("N"));
        var accessibleDirectory = Directory.CreateDirectory(
            Path.Combine(root, "Accessible")).FullName;
        var deniedDirectory = Directory.CreateDirectory(
            Path.Combine(root, "Denied")).FullName;
        var accessibleFile = Path.Combine(accessibleDirectory, "context.md");
        File.WriteAllText(accessibleFile, "calculator runtime context");

        try
        {
            var files = WorkspaceSearchSupport.EnumerateSearchFiles(
                    root,
                    extensions: null,
                    excludedPaths: null,
                    enumerateDirectoryEntries: Enumerate)
                .ToArray();

            Assert.Equal([accessibleFile], files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        IReadOnlyList<string> Enumerate(string directory)
            => string.Equals(directory, deniedDirectory, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Simulated denied RAG directory.")
                : Directory.EnumerateFileSystemEntries(directory).ToArray();
    }

    [Fact]
    public void TokenizeRagQuery_RemovesLowSignalExactResponseTerms()
    {
        var terms = WorkspaceSearchSupport.TokenizeRagQuery(
            "AGENT-THINK-FALSE-OK. Reply exactly with AGENT-THINK-FALSE-OK and do not call tools.");

        Assert.Equal(["AGENT-THINK-FALSE-OK"], terms);
        Assert.False(WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
    }

    [Fact]
    public void TokenizeRagQuery_KeepsDomainTermsForSourceRetrieval()
    {
        var terms = WorkspaceSearchSupport.TokenizeRagQuery(
            "Summarize the x-ray machine quotation and financial workbook assumptions.");

        Assert.Contains("x-ray", terms);
        Assert.Contains("machine", terms);
        Assert.Contains("quotation", terms);
        Assert.Contains("financial", terms);
        Assert.Contains("workbook", terms);
        Assert.True(WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
    }

    [Fact]
    public void CountWholeTermOccurrences_DoesNotMatchInsideOtherWords()
    {
        const string text = "The Tetris game stores the best score locally and should not use a backend.";

        Assert.Equal(0, WorkspaceSearchSupport.CountWholeTermOccurrences(text, "call"));
        Assert.Equal(1, WorkspaceSearchSupport.CountWholeTermOccurrences(text, "backend"));
    }

    [Fact]
    public void ExtractUserRequestForRag_RemovesContextualProjectWrapper()
    {
        const string query = """
            Context:
            - Workspace: project structure.
            - Selected project id: f28c07cd-982c-4d2d-bcf2-3e60a32eca72.
            - Selected project-structure node ids: custom:a7bbaa78a950420086616a910909e037.
            - Treat "this project" and "selected project" as that project structure.

            User request:
            UI-RAG-GATE-OK. Reply exactly with UI-RAG-GATE-OK and do not call tools.
            """;

        var extracted = WorkspaceSearchSupport.ExtractUserRequestForRag(query);
        var terms = WorkspaceSearchSupport.TokenizeRagQuery(query);

        Assert.StartsWith("UI-RAG-GATE-OK.", extracted);
        Assert.Equal(["UI-RAG-GATE-OK"], terms);
        Assert.False(WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
    }

    [Fact]
    public void TokenizeRagQuery_KeepsUserRequestDomainTermsInsideContextualProjectWrapper()
    {
        const string query = """
            Context:
            - Workspace: project structure.
            - Selected project id: f28c07cd-982c-4d2d-bcf2-3e60a32eca72.
            - Selected project-structure node ids: custom:a7bbaa78a950420086616a910909e037.

            User request:
            Summarize the x-ray machine quotation and financial workbook assumptions.
            """;

        var terms = WorkspaceSearchSupport.TokenizeRagQuery(query);

        Assert.Contains("x-ray", terms);
        Assert.Contains("quotation", terms);
        Assert.Contains("financial", terms);
        Assert.DoesNotContain("project", terms);
        Assert.DoesNotContain("structure", terms);
    }
}
