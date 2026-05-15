using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Components;

public sealed class WorkflowExecutorCanvasCatalogTests
{
    [Fact]
    public void BuildQuickCreateActions_groups_plugin_executors_under_plugin_menu()
    {
        var builtIn = CreateExecutor(
            "builtin.utility",
            "Built-in utility",
            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"));
        var officeDownload = CreateExecutor(
            "office365.download",
            "Office365 download",
            WorkflowExecutorSourceDescriptor.BundledPlugin(
                "office365.mail",
                "1.0.0",
                "Office365 Mail",
                UiIconDescriptor.MaterialIcon("business_center", "Office365 Mail")));
        var officeMark = CreateExecutor(
            "office365.mark",
            "Office365 mark processed",
            WorkflowExecutorSourceDescriptor.BundledPlugin(
                "office365.mail",
                "1.0.0",
                "Office365 Mail",
                UiIconDescriptor.MaterialIcon("business_center", "Office365 Mail")));

        var actions = WorkflowExecutorCanvasCatalog.BuildQuickCreateActions(
            [officeMark, builtIn, officeDownload],
            []);

        var executorsMenu = Assert.Single(actions);
        var builtInActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(builtIn.Id);
        var officeDownloadActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeDownload.Id);
        var pluginMenu = Assert.Single(executorsMenu.Children, item => item.ActionId == "workflow-executor:plugins");
        var officeGroup = Assert.Single(pluginMenu.Children, item => item.ActionId == "workflow-executor:plugins:office365.mail");

        Assert.Contains(executorsMenu.Children, item => item.ActionId == builtInActionId);
        Assert.DoesNotContain(executorsMenu.Children, item => item.ActionId == officeDownloadActionId);
        Assert.Equal("Office365 Mail", officeGroup.Label);
        Assert.Equal("business_center", officeGroup.Icon);
        Assert.Contains(officeGroup.Children, item => item.ActionId == officeDownloadActionId);
        Assert.Contains(officeGroup.Children, item => item.ActionId == WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeMark.Id));
    }

    private static WorkflowExecutorDescriptor CreateExecutor(
        string id,
        string name,
        WorkflowExecutorSourceDescriptor source)
        => new(
            new WorkflowExecutorId(id),
            name,
            $"{name} description.",
            WorkflowExecutorCategoryKind.Utility,
            "bolt",
            "test-renderer",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            "{}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = source
        };
}
