using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplateCatalogServiceTests
{
    [Fact]
    public void ListProcessTemplates_returns_current_catalog_items()
    {
        var service = new ProcessTemplateCatalogService(new ProcessTemplatePackLoader());

        var items = service.ListProcessTemplates();

        Assert.Equal(9, items.Count);
        Assert.Contains(items, item => item.Key == "software-delivery");
        Assert.Contains(items, item => item.Key == "branching-code-review");
        Assert.Contains(items, item => item.Key == "oss-intake-supply-chain-governance");
    }

    [Fact]
    public void TryCreateRoleDraft_uses_pack_backed_role_template()
    {
        var service = new ProcessTemplateCatalogService(new ProcessTemplatePackLoader());

        var ok = service.TryCreateRoleDraft("process-role.product-owner", 3, out var role);

        Assert.True(ok);
        Assert.Equal("Product owner 3", role.DisplayName);
        Assert.Contains("value", role.Purpose, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(role.SnapshotSummary));
    }

    [Fact]
    public void TryCreateStepDraft_uses_current_decision_template()
    {
        var service = new ProcessTemplateCatalogService(new ProcessTemplatePackLoader());

        var ok = service.TryCreateStepDraft("process-step.decision", 2, null, 120, 80, out var step);

        Assert.True(ok);
        Assert.Equal(120, step.CanvasX);
        Assert.Equal(80, step.CanvasY);
        Assert.Equal(ProcessStepKind.Decision, step.StepKind);
        Assert.NotEmpty(step.BranchOutcomes);
    }
}
