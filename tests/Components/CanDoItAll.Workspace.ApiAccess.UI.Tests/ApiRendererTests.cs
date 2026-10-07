using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiRendererTests {
    [Fact]
    public async Task Real_form_preserves_raw_lifetime_and_scope_cancel_without_blur() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        using var context = Context();
        var cut = context.Render<ApiAccessSurface>(parameters => parameters.Add(component => component.Session, session));
        var form = session.Issuance!.Form;
        await cut.InvokeAsync(() => {
            cut.Find("#api-token-lifetime").Input("1.");
            cut.Find("[data-testid=api-token-scopes]").Input("fixture.read, incomplete");
        });
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal("1.", cut.Find("#api-token-lifetime").GetAttribute("value"));
        Assert.Contains("whole number", cut.Markup);
        Assert.Equal(0, store.DurableWrites);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-scopes-open]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-scopes-dialog]").QuerySelectorAll("button").Single(button => button.TextContent == "Clear").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-scopes-dialog]").QuerySelectorAll("button").Single(button => button.TextContent == "Cancel").ClickAsync(new()));
        Assert.Equal("fixture.read, incomplete", session.Issuance.Draft.ScopeText);
        Assert.Equal("fixture.read, incomplete", cut.Find("[data-testid=api-token-scopes]").GetAttribute("value"));
        Assert.Same(form, session.Issuance.Form);
    }

    [Fact]
    public async Task Disclosure_dismiss_and_access_retry_remove_sensitive_DOM_and_retire_password_editor() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        using var context = Context();
        var cut = context.Render<ApiAccessSurface>(parameters => parameters.Add(component => component.Session, session));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.StartsWith("NOT-A-CREDENTIAL-", cut.Find("[data-testid=api-issued-token]").TextContent);
        Assert.NotNull(cut.FindComponent<CopyButton>());
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-token-dismiss]").ClickAsync(new()));
        Assert.Empty(cut.FindAll("[data-testid=api-issued-token]"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-create]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-password]").Input("private-sentinel-material"));
        var draft = session.Accounts!.Editor!;
        Assert.Equal("private-sentinel-material", draft.Password);
        await cut.InvokeAsync(session.LoadAsync);
        Assert.Empty(draft.Password);
        Assert.Empty(cut.FindAll("[data-testid=api-user-password]"));
        Assert.DoesNotContain("private-sentinel-material", cut.Markup);
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Retired_scope_dialog_cannot_modify_a_new_account_editor() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        using var context = Context();
        var cut = context.Render<ApiAccessSurface>(parameters => parameters.Add(component => component.Session, session));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-create]").ClickAsync(new()));
        var first = session.Accounts!.Editor!;
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-scopes]").ClickAsync(new()));
        var callback = cut.FindComponent<ApiScopePickerDialog>().Instance.Confirmed;
        Assert.Equal(2, cut.FindAll("[data-testid=api-scope-option]").Count);
        await cut.InvokeAsync(() => {
            session.Accounts.Close(first.Origin);
            session.Accounts.Create();
        });
        Assert.Empty(cut.FindAll("[data-testid=api-scopes-dialog]"));
        await cut.InvokeAsync(() => callback.InvokeAsync(ApiScenarioStore.WriteScope));
        Assert.Empty(session.Accounts.Editor!.ScopeText);
        Assert.NotEqual(first.Origin, session.Accounts.Editor.Origin);
    }

    [Fact]
    public async Task Closed_token_parent_retires_its_confirmation_and_late_write_cannot_close_successor_or_unrelated_dialog() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        using var context = Context();
        var unrelated = context.Render<Dialog>(parameters => parameters.Add(component => component.IsOpen, true).Add(component => component.Title, "Unrelated dialog"));
        var cut = context.Render<ApiAccessSurface>(parameters => parameters.Add(component => component.Session, session));
        Assert.Equal(0, store.Calls.GetValueOrDefault(ApiScenarioOperation.TokenSearch));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-open]").ClickAsync(new()));
        var firstDialog = cut.FindComponent<ApiTokensDialog>();
        await cut.InvokeAsync(() => cut.FindAll("[data-testid=api-token-revoke]").First(button => !button.HasAttribute("disabled")).ClickAsync(new()));
        var confirmation = firstDialog.Instance.Controller.Confirmation!;
        var gate = store.HoldNext(ApiScenarioOperation.RevokeToken);
        Task writing = Task.CompletedTask;
        await cut.InvokeAsync(() => { writing = cut.Find("[data-testid=api-token-confirm]").ClickAsync(new()); });
        await gate.Entered.Task;
        await cut.InvokeAsync(() => firstDialog.Instance.OnClose.InvokeAsync());
        Assert.Empty(cut.FindAll("[data-testid=api-token-confirmation]"));
        Assert.True(unrelated.Instance.IsOpen);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-open]").ClickAsync(new()));
        var successor = cut.FindComponent<ApiTokensDialog>().Instance.Controller;
        var other = successor.Page.Page!.Items.First(token => token.Id != confirmation.Token.Id);
        await cut.InvokeAsync(() => successor.Confirm(other, ApiWriteAction.DeleteToken));
        var nextConfirmation = successor.Confirmation;
        gate.Release();
        await writing;
        Assert.Same(nextConfirmation, successor.Confirmation);
        Assert.Contains(other.DisplayName, cut.Find("[data-testid=api-token-confirmation]").TextContent);
        Assert.True(unrelated.Instance.IsOpen);
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Token_search_uses_immediate_text_and_retained_rows_show_their_original_query() {
        var store = new ApiScenarioStore(ApiScenario.MultiplePages);
        using var authority = new ApiViewLifetime();
        using var controller = new ApiTokenListController(store, authority, new());
        await controller.Page.RefreshAsync();
        using var context = Context();
        var cut = context.Render<ApiTokensDialog>(parameters => parameters.Add(component => component.Controller, controller));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-search]").Input("fixture-0"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-search]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));
        Assert.Equal("fixture-0", controller.Page.AcceptedQuery!.Search);
        Assert.Equal(10, controller.Page.Page!.TotalCount);
        store.FaultNext(ApiScenarioOperation.TokenSearch, ApiScenarioFault.Unavailable);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-search]").Input("missing"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-search-submit]").ClickAsync(new()));
        Assert.Contains("fixture-0", cut.Find("[data-testid=api-tokens-stale]").TextContent);
        Assert.Contains("fixture-0", cut.Find("[data-testid=api-tokens-page]").TextContent);
        Assert.Equal(10, controller.Page.Page.TotalCount);
        Assert.All(cut.FindAll("[data-testid=api-token-delete]"), button => Assert.True(button.HasAttribute("disabled")));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
