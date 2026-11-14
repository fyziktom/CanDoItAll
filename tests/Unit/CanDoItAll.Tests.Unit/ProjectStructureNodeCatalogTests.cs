using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNodeCatalogTests
{
    [Fact]
    public void Generic_group_is_discoverable_without_file_or_runtime_requirements() {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            ProjectStructureCanvasCatalog.GroupActionId, out var definition));
        Assert.Equal(ProjectObjectType.ProjectBlock, definition.ObjectType);
        Assert.Empty(definition.ObjectSubtype);
        Assert.Empty(definition.InputFields ?? []);
        Assert.False(definition.RequiresFile);
        Assert.True(definition.ShowDefaultTextFields);

        foreach (var sourceType in Enum.GetValues<ProjectObjectType>()) {
            Assert.Contains(ProjectStructureCanvasCatalog.BuildMenuCreateActions(sourceType),
                action => action.ActionId == definition.ActionId);
        }
        foreach (var groupKey in new[] { "capture", "blocks" }) {
            var group = Assert.Single(ProjectStructureCanvasCatalog.BuildInspectorCreateGroups(null),
                item => item.Key == groupKey);
            Assert.Contains(group.Actions, action => action.ActionId == definition.ActionId);
        }
        Assert.Contains(ProjectStructureCanvasCatalog.BuildCommonBlockTypeOptions(),
            option => option.ActionId == definition.ActionId && option.ObjectSubtype.Length == 0);
        Assert.Contains(ProjectStructureCanvasCatalog.BuildNoteConversionOptions(),
            option => option.ActionId == definition.ActionId && option.ObjectSubtype.Length == 0);
        Assert.Contains(ProjectStructureCanvasCatalog.BuildAgentNodeCatalog().Items,
            item => item.ActionId == definition.ActionId && item.ObjectType == ProjectObjectType.ProjectBlock && item.ObjectSubtype.Length == 0);
    }

    [Fact]
    public void Composer_sections_have_consistent_identity_and_never_duplicate_input_keys() {
        foreach (var item in ProjectStructureCanvasCatalog.BuildAgentNodeCatalog().Items) {
            Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(item.ActionId, out var definition));
            var fields = ProjectStructureCanvasCatalog.BuildComposerAction(definition).InputFields;
            Assert.Equal(fields.Count, fields.Select(field => field.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var section in fields.GroupBy(field => field.SectionKey)) {
                Assert.False(string.IsNullOrWhiteSpace(section.Key));
                Assert.All(section, field => Assert.False(string.IsNullOrWhiteSpace(field.SectionTitle)));
                Assert.Single(section.Select(field => (field.SectionTitle, field.SectionDescription)).Distinct());
            }
        }
    }

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
