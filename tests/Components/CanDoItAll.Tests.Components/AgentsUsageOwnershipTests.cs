using System.Security.Claims;
using Bunit;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.UI.Shell;
using CanDoItAll.AgentFramework.UI.Usage;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public enum UsageOwnerRetirement { Parent, Profile, Actor }

public sealed class AgentsUsageOwnershipTests {
    public static IEnumerable<object[]> Retirements() =>
        from detail in Enum.GetValues<AgentsOverviewDetail>()
        from retirement in Enum.GetValues<UsageOwnerRetirement>()
        from failLate in new[] { false, true }
        select new object[] { detail, retirement, failLate };

    [Theory]
    [MemberData(nameof(Retirements))]
    public async Task Held_native_read_cannot_publish_after_its_owner_retires(AgentsOverviewDetail detail, UsageOwnerRetirement retirement, bool failLate) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        using var parent = new CancellationTokenSource();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var query = new ProviderUsageQuery(ProviderUsageWorkloadSelection.Agents, ProviderUsagePeriod.SevenDays, DateTimeOffset.UtcNow);
        var authentication = Actor("original");
        var cut = fixture.Harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(p => p
            .Add(c => c.Value, authentication)
            .AddChildContent<UsageDetailHost>(host => host.Add(c => c.Query, query).Add(c => c.Detail, detail).Add(c => c.OwnerLifetime, parent.Token)));
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Agents.Reads));
        var token = fixture.Agents.LastToken;
        await cut.InvokeAsync(() => {
            switch (retirement) {
                case UsageOwnerRetirement.Parent:
                    parent.Cancel();
                    break;
                case UsageOwnerRetirement.Profile:
                    fixture.Harness.Context.Services.GetRequiredService<IDatabaseSwitchNotificationService>()
                        .Publish(new(null, null, Guid.NewGuid(), "owned-test-profile", 2));
                    break;
                case UsageOwnerRetirement.Actor:
                    cut.Render(p => p.Add(c => c.Value, Actor("replacement"))
                        .AddChildContent<UsageDetailHost>(host => host.Add(c => c.Query, query).Add(c => c.Detail, detail).Add(c => c.OwnerLifetime, parent.Token)));
                    break;
            }
        });
        Assert.True(token.IsCancellationRequested);
        if (failLate) {
            await cut.InvokeAsync(() => pending.SetException(new IOException(OverviewPageFixture.PrivateFailure)));
        } else {
            await cut.InvokeAsync(() => pending.SetResult(fixture.Agents.Result("Retired private result")));
        }
        cut.WaitForAssertion(() => Assert.Contains("This usage view has closed", cut.Markup));
        Assert.DoesNotContain("Retired private result", cut.Markup);
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
        Assert.Single(cut.FindAll("[data-testid='usage-detail-close']"));
        Assert.Empty(cut.FindAll("[data-testid='usage-detail-retry']"));
        Assert.Equal(1, fixture.Agents.Reads);
    }

    [Theory]
    [InlineData(AgentsOverviewDetail.Consumers)]
    [InlineData(AgentsOverviewDetail.Providers)]
    [InlineData(AgentsOverviewDetail.Models)]
    public async Task Retry_uses_the_original_window_and_rejects_a_queued_old_intent(AgentsOverviewDetail detail) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Agents.Read = _ => Task.FromException<ProviderUsageSourceResult>(new IOException(OverviewPageFixture.PrivateFailure));
        var query = new ProviderUsageQuery(ProviderUsageWorkloadSelection.Agents, ProviderUsagePeriod.Quarter, DateTimeOffset.UtcNow);
        var cut = fixture.Harness.Context.Render<UsageDetailHost>(p => p.Add(c => c.Query, query).Add(c => c.Detail, detail));
        cut.WaitForElement("[data-testid='usage-detail-retry']");
        var old = cut.FindComponent<UsageDetailFrame>().Instance.State;
        var callback = cut.FindComponent<UsageDetailFrame>().Instance.Intent;
        fixture.Agents.Read = _ => Task.FromResult(fixture.Agents.Result("Accepted retry"));
        await cut.Find("[data-testid='usage-detail-retry']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<UsageDetailFrame>().Instance.State.Snapshot));
        Assert.Equal(query, cut.FindComponent<UsageDetailFrame>().Instance.State.Snapshot!.Query);
        await cut.InvokeAsync(() => callback.InvokeAsync(new(old.Origin, old.Query, UsageDetailAction.Retry)));
        await cut.InvokeAsync(() => callback.InvokeAsync(new(old.Origin, old.Query, UsageDetailAction.Close)));
        Assert.False(cut.FindComponent<UsageDetailFrame>().Instance.State.Retired);
        Assert.Equal(2, fixture.Agents.Reads);
        cut.Render();
        Assert.Equal(2, fixture.Agents.Reads);
    }

    [Fact]
    public async Task Native_shell_rejects_old_tab_origin_without_reading_usage_again() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var page = fixture.Render();
        page.WaitForDashboardLoaded();
        var shell = page.FindComponent<AgentsShellSurface>().Instance;
        var origin = shell.State.Origin;
        var callback = shell.Intent;
        await page.InvokeAsync(() => callback.InvokeAsync(new AgentsShellIntent.SelectTab(origin, "diagnostics")));
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        var location = navigation.Uri;
        var reads = fixture.Agents.Reads;
        await page.InvokeAsync(() => callback.InvokeAsync(new AgentsShellIntent.Command(origin, AgentsShellCommand.OpenWorkflows)));
        Assert.Equal(location, navigation.Uri);
        Assert.Equal(reads, fixture.Agents.Reads);
    }

    private static Task<AuthenticationState> Actor(string name) => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, name)], "owned-test"))));
}
