using CanDoItAll.AgentFramework.Maf;

namespace CanDoItAll.Tests.Unit;

public sealed class MafWorkspaceSearchSupportTests
{
    [Fact]
    public void TokenizeRagQuery_RemovesLowSignalExactResponseTerms()
    {
        var terms = MafAgentRuntime.WorkspaceSearchSupport.TokenizeRagQuery(
            "AGENT-THINK-FALSE-OK. Reply exactly with AGENT-THINK-FALSE-OK and do not call tools.");

        Assert.Equal(["AGENT-THINK-FALSE-OK"], terms);
        Assert.False(MafAgentRuntime.WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
    }

    [Fact]
    public void TokenizeRagQuery_KeepsDomainTermsForSourceRetrieval()
    {
        var terms = MafAgentRuntime.WorkspaceSearchSupport.TokenizeRagQuery(
            "Summarize the x-ray machine quotation and financial workbook assumptions.");

        Assert.Contains("x-ray", terms);
        Assert.Contains("machine", terms);
        Assert.Contains("quotation", terms);
        Assert.Contains("financial", terms);
        Assert.Contains("workbook", terms);
        Assert.True(MafAgentRuntime.WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
    }

    [Fact]
    public void CountWholeTermOccurrences_DoesNotMatchInsideOtherWords()
    {
        const string text = "The Tetris game stores the best score locally and should not use a backend.";

        Assert.Equal(0, MafAgentRuntime.WorkspaceSearchSupport.CountWholeTermOccurrences(text, "call"));
        Assert.Equal(1, MafAgentRuntime.WorkspaceSearchSupport.CountWholeTermOccurrences(text, "backend"));
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

        var extracted = MafAgentRuntime.WorkspaceSearchSupport.ExtractUserRequestForRag(query);
        var terms = MafAgentRuntime.WorkspaceSearchSupport.TokenizeRagQuery(query);

        Assert.StartsWith("UI-RAG-GATE-OK.", extracted);
        Assert.Equal(["UI-RAG-GATE-OK"], terms);
        Assert.False(MafAgentRuntime.WorkspaceSearchSupport.HasEnoughRagSignal(terms, minimumTermCount: 2));
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

        var terms = MafAgentRuntime.WorkspaceSearchSupport.TokenizeRagQuery(query);

        Assert.Contains("x-ray", terms);
        Assert.Contains("quotation", terms);
        Assert.Contains("financial", terms);
        Assert.DoesNotContain("project", terms);
        Assert.DoesNotContain("structure", terms);
    }
}
