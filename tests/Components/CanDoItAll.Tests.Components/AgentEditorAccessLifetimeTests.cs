using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorAccessLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_retry_keeps_unsaved_references_and_ignores_retired_results(bool retire) {
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var reads = context.Services.GetRequiredService<AgentEditorReadFixture>();
        reads.SecretError = "Controlled metadata failure";
        var secret = new AgentAllowedSecretReference(Guid.NewGuid(), "Stored reference", "stored-purpose");
        var draft = new AgentEditorModel { Name = "Before retry", AllowedSecretReferences = [secret] };
        var cut = context.RenderEditor(draft, AgentEditorSection.Secrets);
        var edit = cut.FindComponent<EditForm>().Instance.EditContext;
        var pending = new TaskCompletionSource<IReadOnlyList<AgentEditorSecret>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reads.ReadSecrets = _ => pending.Task;
        var retry = cut.Find("[data-testid='agents-catalog-secret-retry']").ClickAsync();
        if (retire) {
            await cut.FindComponent<StickyActionFooter>().FindAll("button").Single(button => button.TextContent.Trim() == "Clear").ClickAsync();
        } else {
            await cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(AgentEditorSection.Identity)].ClickAsync();
            cut.Find("[data-testid='agents-catalog-name']").Input("Typed during secret read 東京");
        }
        await cut.InvokeAsync(() => pending.SetResult([new(secret.SecretId, "Late secret metadata", "Token")]));
        await retry;
        await cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(AgentEditorSection.Secrets)].ClickAsync();
        var current = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        if (retire) {
            Assert.NotSame(edit, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Empty(current.AllowedSecretReferences);
            Assert.DoesNotContain("Late secret metadata", cut.Markup);
        } else {
            Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Equal("Typed during secret read 東京", current.Name);
            Assert.Equal(secret, Assert.Single(current.AllowedSecretReferences));
            Assert.Contains("Late secret metadata", cut.Markup);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_load_cannot_replace_the_first_acquired_draft_or_its_input(bool fail) {
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var reads = context.Services.GetRequiredService<AgentEditorReadFixture>();
        reads.Load = (_, _) => Task.FromException<AgentEditorLoadResult>(new IOException("Initial controlled failure"));
        var cut = context.Render<AgentDetailsDialog>();
        var retry = cut.FindComponent<AgentEditorCoreSurface>().Instance.State.RetryLoad;
        var first = new TaskCompletionSource<AgentEditorLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<AgentEditorLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        reads.Load = (_, _) => ++calls == 1 ? first.Task : second.Task;
        var oldLoad = cut.InvokeAsync(() => retry.InvokeAsync());
        var currentLoad = cut.InvokeAsync(() => retry.InvokeAsync());
        await cut.InvokeAsync(() => second.SetResult(reads.Result(new() { Name = "Current result" })));
        await currentLoad;
        var edit = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='agents-catalog-name']").Input("Newer raw 東京");
        await cut.InvokeAsync(() => {
            if (fail) {
                first.SetException(new IOException("Obsolete private failure"));
            } else {
                first.SetResult(reads.Result(new() { Name = "Obsolete result" }));
            }
        });
        await oldLoad;
        Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Newer raw 東京", ((AgentEditorModel)edit!.Model).Name);
        Assert.DoesNotContain("Obsolete", cut.Markup);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Risk_acceptance_is_bound_to_permission_revision_and_original_editor(bool environment, bool retire) {
        using var context = AgentDetailsDialogAvatarGenerationTests.CreateContext(new UnavailableAgentImageGenerationService());
        var host = context.Render<DialogHost>();
        var draft = new AgentEditorModel { Name = "Original" };
        draft.WorkspaceToolAccess.CanRunLocalScripts = environment;
        var cut = context.RenderEditor(draft, AgentEditorSection.WorkspaceTools);
        var testId = environment ? "agents-catalog-workspace-scripts-environment" : "agents-catalog-workspace-scripts";
        var prefix = environment ? "agents-workspace-environment-confirmation" : "agents-workspace-scripts-confirmation";
        var pending = cut.Find($"[data-testid='{testId}']").ChangeAsync(true);
        var dialog = host.WaitForComponent<AgentWorkspaceRiskConfirmationDialog>();
        var oldResult = host.FindComponent<AgentWorkspaceRiskConfirmation>().Instance.Completed;
        if (retire) {
            await cut.FindComponent<StickyActionFooter>().FindAll("button").Single(button => button.TextContent.Trim() == "Clear").ClickAsync();
            await pending;
            await cut.InvokeAsync(() => oldResult.InvokeAsync(true));
        } else {
            cut.Find("[data-testid='agents-catalog-workspace-profile']").Change(nameof(AgentWorkspaceToolProfileKind.ReadOnly));
            host.Find($"[data-testid='{prefix}-risk-acknowledgement']").Change(true);
            await host.Find($"[data-testid='{prefix}-confirm']").ClickAsync();
            await pending;
        }
        var current = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        if (retire) {
            await cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(AgentEditorSection.WorkspaceTools)].ClickAsync();
        }
        Assert.False(current.WorkspaceToolAccess.CanRunLocalScripts);
        Assert.False(current.WorkspaceToolAccess.CanScriptsReadEnvironment);
        Assert.False(cut.Find($"[data-testid='{testId}']").HasAttribute("checked"));
        Assert.Equal(retire ? string.Empty : "Original", current.Name);
    }
}
