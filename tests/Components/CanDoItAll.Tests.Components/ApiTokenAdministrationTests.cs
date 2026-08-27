using Bunit;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ApiTokenAdministrationTests {
    [Fact]
    public void TOKEN_SCOPES_confirm_returns_exact_selection_and_cancel_does_not_apply() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        string? confirmed = null;
        var closed = false;
        var cut = context.Render<ApiScopePickerDialog>(parameters => parameters
            .Add(component => component.Value, ApiAccessScopeNames.Api)
            .Add(component => component.Confirmed, value => confirmed = value)
            .Add(component => component.OnClose, () => closed = true));

        Assert.Equal(ApiScopeCatalog.All.Count, cut.FindAll("[data-testid='api-scope-option']").Count);
        cut.FindAll("button").Single(button => button.TextContent == "Clear").Click();
        Assert.True(cut.Find("[data-testid='api-scopes-confirm']").HasAttribute("disabled"));
        cut.Find($"input[value='{ApiAccessScopeNames.ReadSharedProviderCatalog}']").Change(true);
        cut.Find($"input[value='{ApiAccessScopeNames.InvokeSharedProviders}']").Change(true);
        cut.Find("[data-testid='api-scopes-confirm']").Click();
        Assert.Equal($"{ApiAccessScopeNames.ReadSharedProviderCatalog} {ApiAccessScopeNames.InvokeSharedProviders}", confirmed);

        confirmed = null;
        cut.FindAll("button").Single(button => button.TextContent == "Cancel").Click();
        Assert.True(closed);
        Assert.Null(confirmed);
    }

    [Fact]
    public async Task TOKEN_ADMIN_list_is_lazy_and_revoke_delete_require_confirmation() {
        var registry = new RecordingTokenRegistry();
        var access = new TestTokenAdministrationAccess(true);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IApiTokenRegistry>(registry);
            services.AddSingleton<IApiTokenAdministrationAccess>(access);
        });
        var status = harness.Context.Services.GetRequiredService<IApiTokenService>().GetStatus();
        var cut = harness.Context.Render<ApiTokenAdministrationPanel>(parameters => parameters.Add(component => component.Status, status));
        cut.WaitForElement("[data-testid='api-tokens-open']");
        Assert.Equal(0, registry.SearchCount);
        Assert.Empty(cut.FindAll("[data-testid='api-tokens-dialog']"));

        cut.Find("[data-testid='api-tokens-open']").Click();
        cut.WaitForElement("[data-testid='api-token-revoke']");
        Assert.Equal(1, registry.SearchCount);
        cut.Find("[data-testid='api-token-revoke']").Click();
        Assert.Equal(0, registry.RevokeCount);
        cut.Find("[data-testid='api-token-confirmation']").QuerySelectorAll("button")
            .Single(button => button.TextContent == "Cancel").Click();
        Assert.Equal(0, registry.RevokeCount);
        cut.Find("[data-testid='api-token-revoke']").Click();
        cut.Find("[data-testid='api-token-confirm']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, registry.RevokeCount));
        cut.Find("[data-testid='api-token-delete']").Click();
        Assert.Equal(0, registry.DeleteCount);
        cut.Find("[data-testid='api-token-confirm']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, registry.DeleteCount));
    }

    [Fact]
    public async Task TOKEN_ADMIN_access_denial_prevents_data_loading_and_rechecks_every_action() {
        var registry = new RecordingTokenRegistry();
        var access = new TestTokenAdministrationAccess(false);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IApiTokenRegistry>(registry);
            services.AddSingleton<IApiTokenAdministrationAccess>(access);
        });
        var cut = harness.Context.Render<ApiTokenAdministrationPanel>(parameters => parameters
            .Add(component => component.Status, harness.Context.Services.GetRequiredService<IApiTokenService>().GetStatus()));
        cut.WaitForElement("[data-testid='api-token-access-denied']");
        Assert.Equal(0, registry.SearchCount);
        var administration = harness.Context.Services.GetRequiredService<ApiTokenAdministrationService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administration.SearchAsync(new ApiTokenQuery()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administration.IssueAsync(new ApiTokenIssueRequest()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administration.RevokeAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administration.DeleteAsync(Guid.NewGuid()));
        Assert.Equal(0, registry.SearchCount);
        Assert.Equal(0, registry.RevokeCount);
        Assert.Equal(0, registry.DeleteCount);
    }

    [Fact]
    public async Task TOKEN_SCOPES_picker_uses_the_current_textbox_value() {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IApiTokenAdministrationAccess>(new TestTokenAdministrationAccess(true)));
        var cut = harness.Context.Render<ApiTokenAdministrationPanel>(parameters => parameters
            .Add(component => component.Status, harness.Context.Services.GetRequiredService<IApiTokenService>().GetStatus()));
        cut.WaitForElement("[data-testid='api-token-scopes']").Change(ApiAccessScopeNames.InvokeSharedProviders);
        cut.Find("[data-testid='api-scopes-open']").Click();
        cut.WaitForAssertion(() => {
            Assert.Equal(ApiAccessScopeNames.InvokeSharedProviders, cut.FindComponent<ApiScopePickerDialog>().Instance.Value);
            Assert.True(cut.Find($"input[type='checkbox'][value='{ApiAccessScopeNames.InvokeSharedProviders}']").HasAttribute("checked"));
        });
        Assert.False(cut.Find($"input[value='{ApiAccessScopeNames.Api}']").HasAttribute("checked"));
        Assert.DoesNotContain("scopesText", cut.Find("[data-testid='api-scopes-dialog']").TextContent);
    }

    private sealed class TestTokenAdministrationAccess(bool allowed) : IApiTokenAdministrationAccess {
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(allowed);
    }

    private sealed class RecordingTokenRegistry : IApiTokenRegistry {
        private ApiTokenRecord? token = new(Guid.NewGuid(), "desktop", "Desktop token",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [ApiAccessScopeNames.ReadSharedProviderCatalog]);
        public int SearchCount { get; private set; }
        public int RevokeCount { get; private set; }
        public int DeleteCount { get; private set; }
        public void Register(ApiTokenRecord record) => token = record;
        public Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(token);
        public Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) {
            SearchCount++;
            return Task.FromResult(new ApiTokenPage(token is null ? [] : [token], token is null ? 0 : 1));
        }
        public Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) {
            RevokeCount++;
            token = token! with { RevokedAtUtc = revokedAtUtc };
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) {
            DeleteCount++;
            token = null;
            return Task.CompletedTask;
        }
    }
}
