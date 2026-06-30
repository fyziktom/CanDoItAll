using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureNodeCatalogTests
{
    [Fact]
    public void BuildAgentNodeCatalog_includes_work_task_node_and_all_object_types()
    {
        var catalog = ProjectStructureCanvasCatalog.BuildAgentNodeCatalog();

        var workTask = Assert.Single(
            catalog.Items,
            item => item.ObjectType == ProjectObjectType.WorkItem &&
                    item.ObjectSubtype == "task");

        Assert.Equal("add-work-task", workTask.ActionId);
        Assert.Contains("work task", workTask.Aliases, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(catalog.Guidance, item => item.Contains("objectType WorkItem with objectSubtype task", StringComparison.Ordinal));

        foreach (var objectType in Enum.GetValues<ProjectObjectType>())
        {
            Assert.Contains(catalog.ObjectTypes, item => item.ObjectType == objectType);
        }

        Assert.Contains(
            catalog.LinkKinds,
            item => item.Kind == ProjectObjectLinkKind.DependsOn &&
                    item.Guidance.Contains("source node depends on the target node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAgentNodeCatalog_documents_runtime_folder_file_and_git_link_nodes_for_agents()
    {
        var catalog = ProjectStructureCanvasCatalog.BuildAgentNodeCatalog();

        Assert.Contains(catalog.Guidance, item => item.Contains("metadata.repository.localPath", StringComparison.Ordinal));
        Assert.Contains(catalog.Guidance, item => item.Contains("metadata.file.externalPath", StringComparison.Ordinal));
        Assert.Contains(catalog.Guidance, item => item.Contains("GitHub and GitLab URLs", StringComparison.Ordinal));
        Assert.Contains(catalog.Guidance, item => item.Contains("metadata.script.command", StringComparison.Ordinal));
        Assert.Contains(catalog.Guidance, item => item.Contains("metadata.environment.projectPath", StringComparison.Ordinal));
        Assert.Contains(catalog.Guidance, item => item.Contains("metadata.infrastructure.runtimeCommand", StringComparison.Ordinal));
        Assert.Contains(
            catalog.Guidance,
            item => item.Contains("Do not store runnable commands as ProjectBlock delivery nodes", StringComparison.Ordinal));

        AssertCatalogAlias(catalog, ProjectObjectType.Repository, "folder", "folder node");
        AssertCatalogAlias(catalog, ProjectObjectType.Repository, "remote", "github repository");
        AssertCatalogAlias(catalog, ProjectObjectType.Link, string.Empty, "gitlab link");
        AssertCatalogAlias(catalog, ProjectObjectType.Script, "powershell", "powershell runtime");
        AssertCatalogAlias(catalog, ProjectObjectType.Environment, "python", "python runtime");
        AssertCatalogAlias(catalog, ProjectObjectType.Infrastructure, "docker-mode", "docker runtime");
        AssertCatalogAlias(catalog, ProjectObjectType.File, "markdown", "local file");
    }

    private static void AssertCatalogAlias(
        ProjectStructureNodeCatalogResponse catalog,
        ProjectObjectType objectType,
        string objectSubtype,
        string alias)
    {
        var item = Assert.Single(
            catalog.Items,
            item => item.ObjectType == objectType &&
                    item.ObjectSubtype == objectSubtype);

        Assert.Contains(alias, item.Aliases, StringComparer.OrdinalIgnoreCase);
    }
}
