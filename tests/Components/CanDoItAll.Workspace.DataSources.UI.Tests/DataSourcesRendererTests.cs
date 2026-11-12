using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workspace.DataSources.UI;
using CanDoItAll.Workspace.DataSources.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceDataSourcesUi;

public sealed class DataSourcesRendererTests {
    [Fact]
    public async Task Initial_list_completion_preserves_a_newly_acquired_form_and_raw_input() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new DataSourcesScenarioOwner { BeforeListRead = () => gate.Task };
        using var session = new DataSourcesSession(owner, new());
        var initialization = session.InitializeAsync();
        Assert.False(initialization.IsCompleted);
        Assert.False(session.IsLocked);
        using var context = Context();
        var cut = context.Render<DataSourcesPanel>(parameters => parameters.Add(component => component.Session, session));
        await cut.Find("[data-testid='database-profile-new-postgres']").ClickAsync(new());
        var draft = session.Draft;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='database-profile-name']").Change("Acquired while the list is loading");
        cut.Find("[data-testid='database-profile-postgres-port']").Change(string.Empty);
        gate.SetResult();
        await initialization;
        cut.WaitForAssertion(() => {
            Assert.Same(draft, session.Draft);
            Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Equal("Acquired while the list is loading", session.Draft.DisplayName);
            Assert.Equal(string.Empty, cut.Find("[data-testid='database-profile-postgres-port']").GetAttribute("value"));
            Assert.NotEmpty(form!.GetValidationMessages());
        });
        Assert.Null(session.Draft.Id);
        Assert.Empty(owner.Commands);
    }

    [Fact]
    public async Task Real_inputs_keep_raw_validation_non_English_draft_and_form_on_same_row() {
        var owner = new DataSourcesScenarioOwner();
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        using var context = Context();
        var cut = context.Render<DataSourcesPanel>(parameters => parameters.Add(component => component.Session, session));
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='database-profile-name']").Change("Žlutý 東京");
        cut.Find("[data-testid='database-profile-postgres-port']").Change(string.Empty);
        await cut.Find($"[data-testid='database-profile-row-{DataSourcesScenarioOwner.ProfileA:N}']").ClickAsync(new());
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Žlutý 東京", session.Draft.DisplayName);
        Assert.Equal(string.Empty, cut.Find("[data-testid='database-profile-postgres-port']").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();
        Assert.Empty(owner.Commands);
        Assert.NotEmpty(form!.GetValidationMessages());
        Assert.Equal(string.Empty, cut.Find("[data-testid='database-profile-postgres-password']").GetAttribute("value"));
    }

    [Fact]
    public async Task Actual_transfer_controls_require_explicit_group_selection_and_keep_partial_results() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.PartialTransfer);
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        using var context = Context();
        var cut = context.Render<DataSourcesPanel>(parameters => parameters.Add(component => component.Session, session));
        await cut.Find("[data-testid='database-profile-transfer-settings']").ClickAsync(new());
        Assert.True(cut.Find("[data-testid='database-transfer-apply']").HasAttribute("disabled"));
        cut.Find("[data-testid='database-transfer-item-workspace-default-provider'] input").Change(true);
        cut.Find("[data-testid='database-transfer-item-ai-agents'] input").Change(true);
        await cut.Find("[data-testid='database-transfer-apply']").ClickAsync(new());
        Assert.Single(owner.Commands);
        Assert.Contains("Later group failed", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Copied one record", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='database-transfer-apply']").HasAttribute("disabled"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
