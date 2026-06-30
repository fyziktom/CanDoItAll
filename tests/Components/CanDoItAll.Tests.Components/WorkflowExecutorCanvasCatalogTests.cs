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

    [Fact]
    public void BuildCreateAction_includes_side_effect_and_retry_safety_metadata()
    {
        var executor = CreateExecutor(
            "external.write",
            "External write",
            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"),
            WorkflowExecutorSideEffectDescriptor.ExternalWrite(
                WorkflowExecutorExternalMutationKind.None,
                requiresCommitIdempotencyKey: false,
                allowsIdempotentRetry: false,
                idempotencyKeyJsonPath: string.Empty,
                receiptSchema: "{}"),
            new WorkflowExecutorExecutionPolicy(
                TimeoutSeconds: 30,
                MaxRetryAttempts: 2,
                RetryDelayMilliseconds: 250,
                CaptureOutputArtifact: false));

        var action = WorkflowExecutorCanvasCatalog.BuildCreateAction(executor);

        Assert.Contains("Available", action.Description, StringComparison.Ordinal);
        Assert.Contains("External write", action.Description, StringComparison.Ordinal);
        Assert.Contains("Unsafe retries", action.Description, StringComparison.Ordinal);
    }

    private static WorkflowExecutorDescriptor CreateExecutor(
        string id,
        string name,
        WorkflowExecutorSourceDescriptor source,
        WorkflowExecutorSideEffectDescriptor? sideEffects = null,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null)
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
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = source,
            SideEffects = sideEffects ?? WorkflowExecutorSideEffectDescriptor.None
        };
}
