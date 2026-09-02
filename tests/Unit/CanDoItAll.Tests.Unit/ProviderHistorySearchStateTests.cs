using System.ComponentModel.DataAnnotations;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.AgentFramework.Pages.Components.History;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistorySearchStateTests {
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private static ProviderRequestHistoryQuery Query => new(new HistoryProviderScope.AllAuthorized(), Now.AddDays(-1), Now);

    [Fact]
    public async Task Construction_and_draft_edit_do_not_load_and_paging_uses_only_applied_query() {
        var backend = new Backend();
        using var state = Create(backend);
        var draft = new ProviderHistoryFilterDraft(Now) { Model = "exact-model" };
        Assert.Empty(backend.Calls);
        await state.SearchAsync(draft.ToQuery(new HistoryProviderScope.AllAuthorized(), Now));
        draft.Model = "unapplied-model";
        await state.NextAsync();
        Assert.Equal("exact-model", backend.Calls[1].Model!.Value.Value);
        Assert.Equal("page-1", backend.Calls[1].Cursor);
        Assert.Equal(2, state.PageNumber);
        await state.PreviousAsync();
        Assert.Null(backend.Calls[2].Cursor);
        Assert.Equal(1, state.PageNumber);
    }

    [Fact]
    public async Task Reset_and_new_search_discard_old_completion_even_when_backend_ignores_cancellation() {
        var old = new TaskCompletionSource<HistoryPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend { Read = (_, _) => old.Task };
        using var state = Create(backend);
        var pending = state.SearchAsync(Query);
        var oldToken = backend.LastToken;
        state.Reset();
        backend.Read = (_, _) => Task.FromResult(new HistoryPage([], null, new(HistoryCoverageState.Partial, Now), Now));
        await state.SearchAsync(Query with { Model = new("new-query") });
        old.SetResult(new([], "stale-page", new(HistoryCoverageState.Current, Now), Now.AddDays(-1)));
        await pending;
        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal("new-query", state.AppliedQuery!.Model!.Value.Value);
        Assert.Equal(HistoryCoverageState.Partial, state.Page!.Coverage.State);
        Assert.Null(state.Page.NextCursor);
    }

    [Fact]
    public async Task Explicit_cancel_is_distinct_and_late_completion_cannot_restore_the_page() {
        var pending = new TaskCompletionSource<HistoryPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend { Read = (_, _) => pending.Task };
        using var state = Create(backend);
        var read = state.SearchAsync(Query);
        state.Cancel();
        pending.SetResult(new([], null, new(HistoryCoverageState.Current, Now), Now));
        await read;
        Assert.True(backend.LastToken.IsCancellationRequested);
        Assert.True(state.WasCanceled);
        Assert.False(state.IsLoading);
        Assert.Null(state.Page);
    }

    [Fact]
    public async Task Previous_cursor_trail_is_bounded_and_new_search_resets_it() {
        var backend = new Backend();
        using var state = Create(backend);
        await state.SearchAsync(Query);
        for (var index = 0; index < 40; index++) {
            await state.NextAsync();
        }
        Assert.Equal(41, state.PageNumber);
        Assert.True(state.HasEarlierPages);
        for (var index = 0; index < 40; index++) {
            await state.PreviousAsync();
        }
        Assert.Equal(9, state.PageNumber);
        Assert.False(state.CanPrevious);
        await state.SearchAsync(Query);
        Assert.Equal(1, state.PageNumber);
        Assert.False(state.HasEarlierPages);
        Assert.False(state.CanPrevious);
    }

    [Fact]
    public async Task Backend_failures_are_sanitized_and_do_not_display_stale_rows() {
        var backend = new Backend();
        using var state = Create(backend);
        await state.SearchAsync(Query);
        backend.Read = (_, _) => throw new InvalidOperationException("private-test-secret");
        await state.SearchAsync(Query);
        Assert.Null(state.Page);
        Assert.Equal(HistoryFailure.Unavailable, state.Failure);
        Assert.DoesNotContain("private-test-secret", state.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void Invalid_page_size_is_rejected_not_clamped(int pageSize) {
        var draft = new ProviderHistoryFilterDraft(Now) { PageSize = pageSize };
        Assert.Throws<ValidationException>(() => draft.ToQuery(Query.Scope, Now));
        Assert.Equal(pageSize, draft.PageSize);
    }

    [Fact]
    public void Fixed_provider_cannot_be_widened_and_exact_model_identity_is_preserved() {
        var fixedProvider = new ProviderIdentity(Guid.NewGuid());
        var draft = new ProviderHistoryFilterDraft(Now) { ProviderId = Guid.NewGuid().ToString(), Model = "Vendor/Case Sensitive" };
        var query = draft.ToQuery(new HistoryProviderScope.SingleProvider(fixedProvider), Now);
        Assert.Equal(fixedProvider, Assert.IsType<HistoryProviderScope.SingleProvider>(query.Scope).Provider);
        Assert.Equal("Vendor/Case Sensitive", query.Model!.Value.Value);
        Assert.Equal(Now.AddDays(-1), query.FromUtc);
        Assert.Equal(Now, query.ToUtc);
    }

    [Fact]
    public void Invalid_identifier_and_unbounded_custom_interval_are_field_validation_errors() {
        var draft = new ProviderHistoryFilterDraft(Now) { CredentialId = "not-an-id" };
        Assert.Throws<ValidationException>(() => draft.ToQuery(Query.Scope, Now));
        draft.CredentialId = "";
        draft.Range = ProviderHistoryRange.Custom;
        draft.FromUtc = Now.UtcDateTime.AddDays(-32);
        Assert.Throws<ValidationException>(() => draft.ToQuery(Query.Scope, Now));
    }

    [Fact]
    public void Missing_price_and_identity_remain_explicit_without_invented_zero_or_key() {
        Assert.Equal("Unpriced", ProviderHistoryPresentation.Price(new(HistoryPriceState.Unpriced)));
        Assert.Equal("Missing tariff", ProviderHistoryPresentation.Price(new(HistoryPriceState.MissingTariff)));
        Assert.Equal("Caller unavailable", ProviderHistoryPresentation.Caller(new(HistoryAuthenticationKind.Unknown)));
        Assert.Equal("Legacy identity · client", ProviderHistoryPresentation.Caller(new(HistoryAuthenticationKind.LegacyAuthenticated, Subject: "client")));
    }

    private static ProviderHistorySearchState Create(Backend backend) => new(backend, NullLogger<ProviderHistorySearchState>.Instance);

    private sealed class Backend : IProviderRequestHistory {
        internal List<ProviderRequestHistoryQuery> Calls { get; } = [];
        internal CancellationToken LastToken { get; private set; }
        internal Func<ProviderRequestHistoryQuery, CancellationToken, Task<HistoryPage>>? Read { get; set; }
        public Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken cancellationToken) {
            Calls.Add(query);
            LastToken = cancellationToken;
            return Read?.Invoke(query, cancellationToken) ??
                Task.FromResult(new HistoryPage([], $"page-{Calls.Count}", new(HistoryCoverageState.Current, Now), Now));
        }
        public Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entryId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Search must not request metadata detail.");
        public Task<HistoryDetail> GetDetailAsync(HistoryEntryId entryId, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Search must not request content.");
    }
}
