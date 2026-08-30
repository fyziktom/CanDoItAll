using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Pages.Components.History;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderRequestHistoryPanelTests {
    [Fact]
    public void Render_filter_edits_and_controls_do_not_read_until_form_submission() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        Assert.Contains("History not requested", cut.Markup);
        Assert.Equal(["Last 24 hours", "Last 7 days", "Custom"], cut.FindAll("[data-testid='history-range'] option").Select(x => x.TextContent));
        cut.Find("[data-testid='history-model']").Change("Vendor/Exact");
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find("[data-testid='history-external-reference-type']").Change("erp.company-project");
        cut.Find("[data-testid='history-external-reference-value']").Change("company-project-42");
        Assert.Empty(backend.Queries);
        Assert.Equal(0, backend.MetadataReads);
        Assert.Empty(backend.ContentReads);

        cut.Find("[data-testid='history-search-form']").Submit();
        cut.WaitForElement("[data-testid='history-results']");
        var query = Assert.Single(backend.Queries);
        Assert.Equal("Vendor/Exact", query.Model!.Value.Value);
        Assert.Equal("erp.company-project", query.ExternalReference!.Type);
        Assert.Equal("company-project-42", query.ExternalReference.Value);
        Assert.Contains("Coverage is incomplete", cut.Markup);
        Assert.Equal(0, backend.MetadataReads);
        cut.Find("[data-testid='history-model']").Change("New model");
        Assert.Contains("Vendor/Exact", cut.Find("[data-testid='history-applied']").TextContent);
        Assert.Contains("erp.company-project", cut.Find("[data-testid='history-applied']").TextContent);
        Assert.Contains("company-project-42", cut.Find("[data-testid='history-applied']").TextContent);
        cut.Find("[data-testid='history-draft-warning']");
        Assert.Single(backend.Queries);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("201")]
    [InlineData("invalid")]
    public void Invalid_page_size_does_not_search_or_silently_clamp(string value) {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        cut.Find("[data-testid='history-more-filters']").Click();
        cut.Find("[data-testid='history-page-size']").Change(value);
        cut.Find("[data-testid='history-search-form']").Submit();
        Assert.Empty(backend.Queries);
        Assert.NotEmpty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void Provider_scope_changes_clear_rows_and_details_without_another_query() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var first = new HistoryProviderScope.SingleProvider(new(Guid.NewGuid()));
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, first));
        Assert.Empty(cut.FindAll("[data-testid='history-provider']"));
        cut.Find("[data-testid='history-search-form']").Submit();
        cut.WaitForElement("[data-testid='history-details']").Click();
        cut.WaitForElement("[data-testid='history-detail-dialog']");
        Assert.Empty(backend.ContentReads);
        cut.Render(p => p.Add(x => x.Scope, new HistoryProviderScope.SingleProvider(new(Guid.NewGuid()))));
        Assert.Empty(cut.FindAll("[data-testid='history-results'],[data-testid='history-detail-dialog']"));
        Assert.Equal(first, Assert.Single(backend.Queries).Scope);
    }

    [Fact]
    public async Task Profile_change_cancels_search_and_discards_late_results() {
        var backend = new ProviderHistoryUiFixture();
        var pending = new TaskCompletionSource<HistoryPage>();
        CancellationToken observed = default;
        backend.Search = (_, token) => {
            observed = token;
            return pending.Task;
        };
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        var submitted = cut.Find("[data-testid='history-search-form']").SubmitAsync();
        cut.WaitForElement("[data-testid='history-cancel']");
        await cut.InvokeAsync(() => context.Services.GetRequiredService<IDatabaseSwitchNotificationService>()
            .Publish(new(null, null, Guid.NewGuid(), "new-profile", 2)));
        Assert.True(observed.IsCancellationRequested);
        pending.SetResult(new([backend.Entry], null, new(HistoryCoverageState.Current, null), ProviderHistoryUiFixture.Now));
        await submitted;
        Assert.Empty(cut.FindAll("[data-testid='history-results']"));
        Assert.Contains("History not requested", cut.Markup);
    }

    [Fact]
    public void Content_is_separately_requested_and_untrusted_text_is_encoded() {
        var backend = new ProviderHistoryUiFixture();
        backend.Entry = backend.Entry with {
            Usage = new(HistoryUsageState.Partial, 10, 5, CachedInputTokens: 0),
            ExternalReference = new("company-project-42", "erp.company-project")
        };
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        cut.Find("[data-testid='history-search-form']").Submit();
        cut.WaitForElement("[data-testid='history-details']").Click();
        cut.WaitForElement("[data-testid='history-load-content']");
        Assert.Equal(1, backend.MetadataReads);
        Assert.Empty(backend.ContentReads);
        var metadata = cut.Find("[data-testid='history-detail-dialog']").TextContent;
        Assert.Contains("Cached input: 0", metadata);
        Assert.Contains("Cache write: Unavailable", metadata);
        Assert.Contains("Reasoning: Unavailable", metadata);
        Assert.Contains("Images: Unavailable", metadata);
        Assert.Contains("Type: erp.company-project", metadata);
        Assert.Contains("Value: company-project-42", metadata);
        cut.Find("[data-testid='history-load-content']").Click();
        cut.WaitForElement("[data-testid='history-content-text']");
        Assert.Single(backend.ContentReads);
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("<script>untrusted()</script>", cut.Find("textarea").TextContent);
        cut.Find("[data-testid='history-detail-close']").Click();
        Assert.Empty(cut.FindAll("[data-testid='history-detail-dialog']"));
    }

    [Fact]
    public void Authentication_change_clears_rows_without_automatic_reload() {
        var backend = new ProviderHistoryUiFixture();
        using var context = backend.CreateContext();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("operator");
        var cut = context.Render<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(p =>
            p.AddChildContent<ProviderRequestHistoryPanel>(child => child.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized())));
        cut.Find("[data-testid='history-search-form']").Submit();
        cut.WaitForElement("[data-testid='history-results']");
        authorization.SetNotAuthorized();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='history-results']")));
        Assert.Single(backend.Queries);
    }

    [Fact]
    public async Task Closing_detail_cancels_inflight_content_and_cannot_reopen_from_a_late_response() {
        var backend = new ProviderHistoryUiFixture();
        var content = new TaskCompletionSource<HistoryDetail>();
        CancellationToken observed = default;
        backend.Content = token => {
            observed = token;
            return content.Task;
        };
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderRequestHistoryPanel>(p => p.Add(x => x.Scope, new HistoryProviderScope.AllAuthorized()));
        cut.Find("[data-testid='history-search-form']").Submit();
        cut.WaitForElement("[data-testid='history-details']").Click();
        var loading = cut.WaitForElement("[data-testid='history-load-content']").ClickAsync(new());
        cut.Find("[data-testid='history-detail-close']").Click();
        Assert.True(observed.IsCancellationRequested);
        content.SetResult(new(backend.Entry.Id, HistoryDetailState.Captured, new("late content", 12, 12, HistoryDetailFlags.None)));
        await loading;
        Assert.Empty(cut.FindAll("[data-testid='history-detail-dialog']"));
        Assert.DoesNotContain("late content", cut.Markup);
    }

    [Fact]
    public void Canonical_owner_is_passed_exactly_and_content_denial_clears_disclosed_metadata() {
        var backend = new ProviderHistoryUiFixture();
        var owner = new CanonicalEvidenceReference(backend.Entry.Partition, HistorySourceKind.SimpleChat, new("conversation"), new("turn"));
        backend.Entry = backend.Entry with { MetadataAuthority = HistoryMetadataAuthority.CanonicalProjection, DetailState = HistoryDetailState.Canonical };
        backend.Owners = [new(backend.Entry.Id, owner, new(1), HistoryOwnerRole.ContentOwner, HistoryOwnerState.Linked)];
        backend.Content = _ => throw new ProviderHistoryException(HistoryFailure.Denied, "Content access denied.");
        using var context = backend.CreateContext();
        var cut = context.Render<ProviderHistoryDetailsDialog>(p => p.Add(x => x.EntryId, backend.Entry.Id));
        cut.WaitForElement("[data-testid='history-owner-content']");
        Assert.Empty(cut.FindAll("[data-testid='history-load-content']"));
        cut.Find("[data-testid='history-owner-content']").Click();
        Assert.Equal(owner, Assert.Single(backend.ContentReads));
        cut.WaitForElement("[data-testid='history-detail-error']");
        Assert.DoesNotContain(backend.Entry.Id.Value.ToString(), cut.Markup);
    }
}
