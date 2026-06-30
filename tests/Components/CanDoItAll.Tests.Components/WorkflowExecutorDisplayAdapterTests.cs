using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Components;

public sealed class WorkflowExecutorDisplayAdapterTests
{
    [Fact]
    public void BuildPreviewCommitBadge_distinguishes_preview_commit_executor()
    {
        var descriptor = CreateDescriptor() with
        {
            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead()
        };

        var badge = WorkflowExecutorDisplayAdapter.BuildPreviewCommitBadge(descriptor);

        Assert.Equal("Preview + commit", badge.Text);
        Assert.Equal("info", badge.Tone);
        Assert.Contains("preview and commit", WorkflowExecutorDisplayAdapter.BuildPreviewCommitDescription(descriptor), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSummaryBadges_includes_preview_commit_status()
    {
        var descriptor = CreateDescriptor() with
        {
            SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker("$.idempotencyKey", "receipt.v1")
        };

        var badges = WorkflowExecutorDisplayAdapter.BuildSummaryBadges(descriptor);

        Assert.Contains("Preview + commit", badges);
        Assert.Contains("Retry safe", badges);
    }

    private static WorkflowExecutorDescriptor CreateDescriptor()
    {
        return new WorkflowExecutorDescriptor(
            Id: new WorkflowExecutorId("test.executor"),
            Name: "Test executor",
            Description: "Test executor.",
            Category: WorkflowExecutorCategoryKind.Utility,
            IconName: "bolt",
            SetupRendererKey: "test",
            InputShape: WorkflowValueShape.Text,
            ResultShape: WorkflowValueShape.Text,
            SettingsSchemaJson: "{}",
            DefaultSettingsJson: "{}",
            DefaultPolicy: WorkflowExecutorExecutionPolicy.Default with
            {
                MaxRetryAttempts = 1
            },
            IsImplemented: true);
    }
}
