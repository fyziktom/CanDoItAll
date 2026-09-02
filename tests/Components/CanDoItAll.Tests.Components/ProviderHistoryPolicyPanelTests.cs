using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderHistoryPolicyPanelTests {
    [Fact]
    public void Policy_load_and_future_only_update_are_explicit_and_versioned() {
        var history = new ProviderHistoryUiFixture();
        var policy = new PolicyBackend();
        using var context = history.CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        Assert.Equal(0, policy.Reads);
        Assert.Empty(history.Queries);
        Assert.Contains("Policy not requested", cut.Markup);
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement("[data-testid='history-policy-form']");
        cut.Find("[data-testid='history-policy-metadata-days']").Change("20");
        Assert.Empty(policy.Updates);
        Assert.Equal(0, policy.Previews);
        cut.Find("[data-testid='history-policy-form']").Submit();
        var update = Assert.Single(policy.Updates);
        Assert.Equal(5, update.ExpectedVersion);
        Assert.Equal(20, update.Policy.MetadataRetentionDays);
        Assert.False(update.ApplyShorterRetention);
        Assert.Contains("Existing expiry dates are unchanged", cut.Markup);
        Assert.Empty(history.Queries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_expiry_changes_require_preview_then_confirmation_and_conflicts_are_visible(bool conflict) {
        var policy = new PolicyBackend { Conflict = conflict };
        using var context = new ProviderHistoryUiFixture().CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement("[data-testid='history-policy-metadata-days']").Change("10");
        cut.Find("[data-testid='history-policy-preview']").Click();
        cut.WaitForElement("[data-testid='history-policy-confirmation']");
        Assert.Equal(1, policy.Previews);
        Assert.Empty(policy.Updates);
        cut.Find("[data-testid='history-policy-confirm']").Click();
        Assert.True(Assert.Single(policy.Updates).ApplyShorterRetention);
        if (conflict) {
            Assert.Contains("Conflict", cut.Find("[data-testid='history-policy-error']").TextContent);
            Assert.Empty(cut.FindAll("[data-testid='history-policy-success']"));
        } else {
            cut.WaitForElement("[data-testid='history-policy-success']");
        }
    }

    [Fact]
    public void Oversized_preview_cannot_apply_existing_retention() {
        var policy = new PolicyBackend { ExceedsLimit = true };
        using var context = new ProviderHistoryUiFixture().CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement("[data-testid='history-policy-preview']").Click();
        Assert.True(cut.Find("[data-testid='history-policy-confirm']").HasAttribute("disabled"));
        cut.Find("[data-testid='history-policy-confirm']").Click();
        Assert.Empty(policy.Updates);
        Assert.Contains("safe batch limit", cut.Markup);
    }

    [Theory]
    [InlineData("metadata-days", "0")]
    [InlineData("detail-days", "40")]
    [InlineData("text-bytes", "131073")]
    [InlineData("quota", "0")]
    [InlineData("batch", "invalid")]
    public void Invalid_policy_values_are_not_clamped_or_submitted(string field, string value) {
        var policy = new PolicyBackend();
        using var context = new ProviderHistoryUiFixture().CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement($"[data-testid='history-policy-{field}']").Change(value);
        cut.Find("[data-testid='history-policy-form']").Submit();
        cut.Find("[data-testid='history-policy-preview']").Click();
        Assert.NotEmpty(cut.FindAll(".validation-message"));
        Assert.Empty(policy.Updates);
        Assert.Equal(0, policy.Previews);
    }

    [Fact]
    public async Task Profile_change_cancels_and_discards_a_late_policy_read() {
        var policy = new PolicyBackend { PendingRead = new() };
        using var context = new ProviderHistoryUiFixture().CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        var pending = cut.Find("[data-testid='history-policy-load']").ClickAsync(new());
        await cut.InvokeAsync(() => context.Services.GetRequiredService<IDatabaseSwitchNotificationService>()
            .Publish(new(null, null, Guid.NewGuid(), "new-profile", 2)));
        Assert.True(policy.LastToken.IsCancellationRequested);
        policy.PendingRead.SetResult(new(new(), 9));
        await pending;
        Assert.Empty(cut.FindAll("[data-testid='history-policy-form']"));
        Assert.Contains("Policy not requested", cut.Markup);
    }

    [Fact]
    public void Authentication_change_clears_loaded_policy_without_an_automatic_reload() {
        var policy = new PolicyBackend();
        using var context = new ProviderHistoryUiFixture().CreateContext();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("operator");
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<CascadingAuthenticationState>(p => p.AddChildContent<ProviderHistoryPolicyPanel>());
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement("[data-testid='history-policy-form']");
        authorization.SetNotAuthorized();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='history-policy-form']")));
        Assert.Equal(1, policy.Reads);
    }

    [Fact]
    public async Task Settings_history_route_has_no_automatic_policy_read_or_workspace_save_form() {
        var policy = new PolicyBackend();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IProviderHistoryPolicyService>(policy));
        harness.Context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/settings?tab=provider-history");
        var cut = harness.Context.Render<CanDoItAll.Modules.Workspace.Pages.SettingsPage>();
        cut.WaitForElement("[data-testid='history-policy-panel']");
        Assert.Equal(0, policy.Reads);
        Assert.Empty(cut.FindAll("form"));
        cut.Find("[data-testid='history-policy-load']").Click();
        cut.WaitForElement("[data-testid='history-policy-form']");
        Assert.Single(cut.FindAll("form"));
        Assert.DoesNotContain("Save defaults", cut.Markup);
    }

    [Fact]
    public void Denied_management_does_not_display_a_policy_editor() {
        var policy = new PolicyBackend { Denied = true };
        using var context = new ProviderHistoryUiFixture().CreateContext();
        context.Services.AddSingleton<IProviderHistoryPolicyService>(policy);
        var cut = context.Render<ProviderHistoryPolicyPanel>();
        cut.Find("[data-testid='history-policy-load']").Click();
        Assert.Contains("Denied", cut.Find("[data-testid='history-policy-error']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='history-policy-form']"));
    }

    private sealed class PolicyBackend : IProviderHistoryPolicyService {
        internal int Reads { get; private set; }
        internal int Previews { get; private set; }
        internal bool Conflict { get; init; }
        internal bool Denied { get; init; }
        internal bool ExceedsLimit { get; init; }
        internal CancellationToken LastToken { get; private set; }
        internal TaskCompletionSource<HistoryPolicySnapshot>? PendingRead { get; init; }
        internal List<HistoryPolicyUpdate> Updates { get; } = [];

        public Task<HistoryPolicySnapshot> GetAsync(CancellationToken cancellationToken) {
            Reads++;
            if (Denied) {
                throw new ProviderHistoryException(HistoryFailure.Denied, "Management access required.");
            }
            LastToken = cancellationToken;
            return PendingRead?.Task ?? Task.FromResult(new HistoryPolicySnapshot(new(), 5));
        }

        public Task<HistoryRetentionPreview> PreviewShorterRetentionAsync(HistoryPolicy policy, CancellationToken cancellationToken) {
            Previews++;
            return Task.FromResult(new HistoryRetentionPreview(3, 2, 500, ExceedsLimit));
        }

        public Task<HistoryPolicySnapshot> UpdateAsync(HistoryPolicyUpdate update, CancellationToken cancellationToken) {
            Updates.Add(update);
            return Conflict
                ? Task.FromException<HistoryPolicySnapshot>(new ProviderHistoryException(HistoryFailure.Conflict, "Reload the current policy."))
                : Task.FromResult(new HistoryPolicySnapshot(update.Policy, update.ExpectedVersion + 1));
        }
    }
}
