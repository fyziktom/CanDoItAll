using Bunit;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Workspace.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class WorkspaceApiStatusTests {
    [Theory]
    [InlineData(false, "Open")]
    [InlineData(true, "JWT")]
    public async Task Reopening_a_failed_status_host_replaces_the_previous_badge_without_eager_reads(bool authorizationEnabled, string initialBadge) {
        var tokens = new StatusService(authorizationEnabled);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IApiTokenService>(tokens));
        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid='defaults-name']");
        cut.WaitForAssertion(() => {
            var state = cut.FindComponent<WorkspaceSettingsSurface>().Instance;
            Assert.False(state.Defaults.IsLoading);
            Assert.False(state.Secrets.IsLoading);
            Assert.True(state.Defaults.CanSave);
        });
        Assert.Equal(0, tokens.Reads);
        Assert.Contains("Not loaded", ApiTab().TextContent);

        await cut.InvokeAsync(() => ApiTab().Click());
        cut.WaitForAssertion(() => Assert.Contains(initialBadge, ApiTab().TextContent));
        Assert.Equal(1, tokens.Reads);
        await SelectWorkspaceAsync();
        Assert.Equal(1, tokens.Reads);

        tokens.Fail = true;
        await cut.InvokeAsync(() => ApiTab().Click());
        cut.WaitForAssertion(() => {
            Assert.Contains("API status is unavailable", cut.Markup);
            Assert.Contains("Unavailable", ApiTab().TextContent);
        });
        Assert.Equal(2, tokens.Reads);
        Assert.DoesNotContain("JWT disabled", cut.Markup);

        tokens.Fail = false;
        await SelectWorkspaceAsync();
        await cut.InvokeAsync(() => ApiTab().Click());
        cut.WaitForAssertion(() => Assert.Contains(initialBadge, ApiTab().TextContent));
        Assert.Equal(3, tokens.Reads);

        AngleSharp.Dom.IElement ApiTab() => cut.FindAll("button").Single(button => button.TextContent.Contains("API Access", StringComparison.Ordinal));
        Task SelectWorkspaceAsync() => cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Workspace").Click());
    }

    private sealed class StatusService(bool authorizationEnabled) : IApiTokenService {
        public int Reads { get; private set; }
        public bool Fail { get; set; }

        public ApiAccessStatus GetStatus() {
            Reads++;
            if (Fail) {
                throw new InvalidOperationException("Controlled status read failure");
            }
            return new(true, true, true, authorizationEnabled, false, "fixture", "fixture", 30, 60);
        }

        public ApiTokenIssueResult IssueToken(ApiTokenIssueRequest request) => throw new InvalidOperationException("Status observation must not issue tokens.");
    }
}
