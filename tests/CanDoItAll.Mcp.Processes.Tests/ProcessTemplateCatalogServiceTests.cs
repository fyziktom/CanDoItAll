using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

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
        Assert.Equal(ProjectPartyAssignmentRole.CustomerContact, role.PreferredProjectAssignmentRole);
        Assert.Contains("Seniority:", role.SnapshotSummary);
        Assert.Contains("Domain tags:", role.SnapshotSummary);
    }

    [Fact]
    public void CreateRoleDraft_matches_library_role_snapshot_summary()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);
        var catalog = new ProcessTemplateCatalogService(loader);
        var library = new ProcessTemplateLibraryService(loader, projection);

        var ok = catalog.TryCreateRoleDraft("process-role.product-owner", 1, out var catalogRole);
        var libraryRole = library.CreateRoleDraft("shared-role:product-owner", 1);

        Assert.True(ok);
        Assert.Equal(catalogRole.SnapshotSummary, libraryRole.SnapshotSummary);
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
