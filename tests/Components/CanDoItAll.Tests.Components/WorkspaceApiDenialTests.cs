using Bunit;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Workspace.ApiAccess.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class WorkspaceApiDenialTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_settings_current_denial_clears_sensitive_children_and_retry_tab_retirement_is_safe(bool tokenList) {
        var access = new ControlledAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.EnableUi().AddSingleton<IApiTokenAdministrationAccess>(access));
        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid=defaults-name]");
        await ApiTab();
        cut.WaitForElement("[data-testid=api-token-create]");
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-token-create]").ClickAsync(new()));
        cut.WaitForElement("[data-testid=api-issued-token]");
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-create]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-password]").Input("synthetic-private-draft"));
        var session = cut.FindComponent<ApiAccessSurface>().Instance.Session;
        var draft = session.Accounts!.Editor!;
        var issuer = session.Issuance!;
        access.Allowed = false;
        await cut.InvokeAsync(() => cut.Find(tokenList ? "[data-testid=api-tokens-open]" : "[data-testid=api-users-refresh]").ClickAsync(new()));
        cut.WaitForElement("[data-testid=api-token-access-denied]");
        Assert.Null(issuer.Disclosure);
        Assert.Empty(draft.Password);
        Assert.Empty(cut.FindAll("[data-testid=api-user-dialog]"));
        Assert.Empty(cut.FindAll("[data-testid=api-tokens-dialog]"));
        Assert.Single(session.Receipts);
        access.Allowed = true;
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-access-retry]").ClickAsync(new()));
        cut.WaitForElement("[data-testid=api-token-create]");
        Assert.Empty(cut.FindAll("[data-testid=api-issued-token]"));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Workspace").ClickAsync(new()));
        cut.WaitForElement("[data-testid=defaults-name]");
        await ApiTab();
        cut.WaitForElement("[data-testid=api-token-create]");
        Assert.Empty(cut.FindAll("[data-testid=api-issued-token]"));
        Assert.Single(cut.FindComponent<ApiAccessSurface>().Instance.Session.Receipts);

        Task ApiTab() => cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Contains("API Access", StringComparison.Ordinal)).ClickAsync(new()));
    }

    private sealed class ControlledAccess : IApiTokenAdministrationAccess {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Allowed);
    }
}
