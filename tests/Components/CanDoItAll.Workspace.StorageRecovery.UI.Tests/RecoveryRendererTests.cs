using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;
using CanDoItAll.Workspace.StorageRecovery.UI;
using CanDoItAll.Workspace.StorageRecovery.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceStorageRecoveryUi;

public sealed class RecoveryRendererTests {
    [Theory]
    [InlineData(RecoveryScenario.WorkflowPrepared, "storage-recovery-complete-workflow-asset")]
    [InlineData(RecoveryScenario.WorkflowReceipt, "storage-recovery-record-workflow-receipt")]
    [InlineData(RecoveryScenario.CancelledRun, "storage-recovery-reconcile-cancelled")]
    public async Task Real_owner_controls_invoke_only_the_captured_offered_action(RecoveryScenario scenario, string actionTestId) {
        var owner = new RecoveryScenarioOwner(scenario);
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        using var context = Context();
        var cut = context.Render<RecoveryDialog>(parameters => parameters.Add(component => component.Session, session));
        await cut.Find($"[data-testid='storage-recovery-inspect-{RecoveryScenarioOwner.FollowUpId:N}']").ClickAsync(new());
        Assert.Empty(owner.Commands);
        var captured = session.Selected!;
        await cut.Find($"[data-testid='{actionTestId}']").ClickAsync(new());
        Assert.Equal(captured.Item.Target, Assert.Single(owner.Commands).Target);
        Assert.Contains("ReceiptRecorded", cut.Find("[data-testid='storage-recovery-acknowledged']").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll($"[data-testid='{actionTestId}']"));
    }

    [Fact]
    public async Task Rebinding_renderer_detaches_the_previous_session_and_keeps_denied_scope_empty() {
        using var first = new RecoverySession(new RecoveryScenarioOwner());
        using var second = new RecoverySession(new RecoveryScenarioOwner(RecoveryScenario.Denied));
        await first.RefreshAsync();
        await first.SelectAsync(first.Placements.Items[0]);
        var detail = first.Selected!;
        using var context = Context();
        var cut = context.Render<RecoveryDialog>(parameters => parameters.Add(component => component.Session, first));
        await second.RefreshAsync();
        cut.Render(parameters => parameters.Add(component => component.Session, second));
        var current = cut.Markup;
        await cut.InvokeAsync(() => first.ExecuteAsync(detail, RecoveryAction.Reconcile));
        Assert.NotNull(first.LastResult);
        Assert.Equal(current, cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='storage-recovery-selected-intent']"));
        Assert.Contains("does not allow", cut.Find("[data-testid='storage-recovery-error']").TextContent, StringComparison.Ordinal);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
