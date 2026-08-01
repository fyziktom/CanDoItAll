using CanDoItAll.AgentFramework.Maf;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceImageAnalysisPromptNormalizerTests
{
    [Fact]
    public void NormalizeSingleImagePrompt_WhenPromptIsEmpty_UsesDomainNeutralVisibleEvidencePrompt()
    {
        var normalized = WorkspaceImageAnalysisPromptNormalizer.NormalizeSingleImagePrompt(" ");

        Assert.Contains("one image file", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only visible evidence", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("directly visible", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("software", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UI", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeSingleImagePrompt_WhenPromptIsProvided_PreservesTrimmedUserQuestion()
    {
        var normalized = WorkspaceImageAnalysisPromptNormalizer.NormalizeSingleImagePrompt("  Check whether the label is readable.  ");

        Assert.Contains("User question: Check whether the label is readable.", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("software", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UI", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeImageSetPrompt_WhenPromptIsEmpty_UsesDomainNeutralComparisonPrompt()
    {
        var normalized = WorkspaceImageAnalysisPromptNormalizer.NormalizeImageSetPrompt(string.Empty, 2, string.Empty);

        Assert.Contains("2 ordered image files", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without assuming the domain or purpose", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible similarities and differences", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("software", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UI", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeImageSetPrompt_WhenDeterministicEvidenceIsProvided_IncludesTrimmedEvidence()
    {
        var normalized = WorkspaceImageAnalysisPromptNormalizer.NormalizeImageSetPrompt(
            "Compare visible changes.",
            3,
            "  Tool-computed pixel evidence from the image files:  ");

        Assert.Contains("3 ordered image files", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tool-computed pixel evidence from the image files:", normalized, StringComparison.Ordinal);
        Assert.Contains("User question: Compare visible changes.", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("software", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UI", normalized, StringComparison.OrdinalIgnoreCase);
    }
}
