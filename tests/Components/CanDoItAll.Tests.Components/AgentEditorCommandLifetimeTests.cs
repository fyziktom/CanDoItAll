using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorCommandLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reset_ignores_late_save_completion(bool fail) {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var pending = new TaskCompletionSource<Guid>();
        probe.Save = _ => pending.Task;
        var completions = new List<AgentDetailsDialogResult>();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agent.Id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this, completions.Add)));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        var submitted = cut.Find("form").SubmitAsync();
        cut.FindComponent<StickyActionFooter>().FindAll("button").Single(button => button.TextContent.Trim() == "Clear").Click();
        cut.WaitForAssertion(() => Assert.Null(((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).Id));
        var resetContext = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.InvokeAsync(() => Complete(pending, agent.Id, fail));
        await submitted;
        Assert.Same(resetContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.True(cut.Instance.CurrentTarget.IsNew);
        Assert.Empty(completions);
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Detail?.Contains("Delayed save failure", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_ignores_late_save_completion(bool fail) {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var pending = new TaskCompletionSource<Guid>();
        probe.Save = _ => pending.Task;
        var completions = new List<AgentDetailsDialogResult>();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agent.Id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this, completions.Add)));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        var submitted = cut.Find("form").SubmitAsync();
        await cut.InvokeAsync(() => {
            cut.Instance.Dispose();
            Complete(pending, agent.Id, fail);
        });
        await submitted;
        Assert.Empty(completions);
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary is "Agent saved" or "Agent save failed" or "Agent save could not be confirmed");
    }

    [Fact]
    public async Task Save_snapshots_draft_and_preserves_later_edits() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var pending = new TaskCompletionSource<Guid>();
        AgentEditorModel? request = null;
        probe.Save = model => {
            request = model;
            return pending.Task;
        };
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agent.Id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Submitted name");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var submitted = cut.Find("form").SubmitAsync();
        cut.Find("[data-testid='agents-catalog-name']").Change("Later edit");
        var submittedNameAfterEdit = request!.Name;
        await cut.InvokeAsync(() => pending.SetResult(agent.Id));
        await submitted;
        Assert.Equal("Submitted name", submittedNameAfterEdit);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Later edit", ((AgentEditorModel)context!.Model).Name);
        Assert.Equal(agent.UpdatedAtUtc, ((AgentEditorModel)context.Model).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Target_echo_after_first_save_retains_session_and_later_edits() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        var host = harness.Context.Render<AgentEditorTargetEchoHost>();
        var cut = host.FindComponent<AgentDetailsDialog>();
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Echo submitted");
        var pending = new TaskCompletionSource();
        probe.Save = async request => {
            await pending.Task;
            return await probe.Target.SaveAgentAsync(request);
        };
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var submitted = cut.Find("form").SubmitAsync();
        cut.Find("[data-testid='agents-catalog-name']").Change("Echo later edit");
        await cut.InvokeAsync(() => pending.SetResult());
        await submitted;
        cut.WaitForAssertion(() => Assert.NotNull(host.Instance.AgentId));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        Assert.Equal(host.Instance.AgentId, cut.Instance.CurrentTarget.AgentId);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Echo later edit", ((AgentEditorModel)context!.Model).Name);
        Assert.NotNull(((AgentEditorModel)context.Model).ExpectedUpdatedAtUtc);
        Assert.Equal(1, host.Instance.Completions);
    }

    [Fact]
    public async Task Reset_cancels_an_inflight_save_without_an_unconfirmed_write_warning() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        var pending = new TaskCompletionSource<Guid>();
        probe.Save = _ => pending.Task;
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Cancelled editor");
        var submitted = cut.Find("form").SubmitAsync();
        await cut.FindComponent<StickyActionFooter>().FindAll("button")
            .Single(button => button.TextContent.Trim() == "Clear").ClickAsync();
        Assert.True(probe.SaveToken.IsCancellationRequested);
        await cut.InvokeAsync(() => pending.SetCanceled(probe.SaveToken));
        await submitted;
        Assert.Empty(cut.FindAll("[data-testid='agents-editor-write-unconfirmed']"));
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.DoesNotContain(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary is "Agent save failed" or "Agent save could not be confirmed");
    }

    private static void Complete(TaskCompletionSource<Guid> pending, Guid id, bool fail) {
        if (fail) {
            pending.SetException(new InvalidOperationException("Delayed save failure."));
        } else {
            pending.SetResult(id);
        }
    }
}
