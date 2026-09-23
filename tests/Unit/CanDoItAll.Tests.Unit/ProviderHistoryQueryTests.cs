using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistoryQueryTests {
    [Fact]
    public void No_query_without_explicit_call() {
        using var fixture = new ProviderHistoryQueryTestContext();
        Assert.Equal(0, fixture.Store.Searches);
        Assert.Equal(0, fixture.Store.ContentReads);
        Assert.Equal(0, fixture.Owner.ContentReads);
    }

    [Fact]
    public async Task Missing_metadata_permission_sends_no_query() {
        using var fixture = new ProviderHistoryQueryTestContext();
        fixture.Authority.Permissions.Remove(HistoryPermission.ReadMetadata);
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(fixture.Query, default));
        Assert.Equal(HistoryFailure.Denied, error.Failure);
        Assert.Equal(0, fixture.Store.Searches);
    }

    [Fact]
    public async Task Host_provider_scope_cannot_be_widened() {
        using var fixture = new ProviderHistoryQueryTestContext();
        fixture.Authority.Context = fixture.Authority.Context with { AllowedProviders = new HashSet<ProviderIdentity> { ProviderHistoryQueryTestContext.Provider } };
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(
            fixture.Query with { Scope = new HistoryProviderScope.SingleProvider(new(Guid.NewGuid())) }, default));
        Assert.Equal(HistoryFailure.Denied, error.Failure);
        Assert.Equal(0, fixture.Store.Searches);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Cursor_is_bound_to_scope_and_filters(int change) {
        using var fixture = new ProviderHistoryQueryTestContext();
        fixture.Store.Page = fixture.Store.Page with { Entries = [ProviderHistoryQueryTestContext.Entry(), ProviderHistoryQueryTestContext.Entry()] };
        var first = await fixture.Service.SearchAsync(fixture.Query, default);
        var next = fixture.Query with { Cursor = first.NextCursor };
        next = change switch {
            0 => next with { Scope = new HistoryProviderScope.SingleProvider(ProviderHistoryQueryTestContext.Provider) },
            1 => next with { Model = new ProviderModelIdentity("another-model") },
            2 => next with { PageSize = 2 },
            3 => next with { CredentialId = new ManagedCredentialId(Guid.NewGuid()) },
            4 => next with { FromUtc = next.FromUtc.AddMinutes(-1) },
            5 => next with { PriceState = HistoryPriceState.ExplicitFree },
            _ => next
        };
        if (change == 6) {
            fixture.Authority.Context = fixture.Authority.Context with { AuthorizationStamp = "revoked-and-regranted" };
        }
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(next, default));
        Assert.Equal(HistoryFailure.InvalidCursor, error.Failure);
        Assert.Equal(1, fixture.Store.Searches);
    }

    [Fact]
    public async Task Tampered_cursor_is_rejected_before_query() {
        using var fixture = new ProviderHistoryQueryTestContext();
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() =>
            fixture.Service.SearchAsync(fixture.Query with { Cursor = "forged" }, default));
        Assert.Equal(HistoryFailure.InvalidCursor, error.Failure);
        Assert.Equal(0, fixture.Store.Searches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Permission_or_profile_change_before_publish_discards_result(bool profile) {
        using var fixture = new ProviderHistoryQueryTestContext();
        fixture.Store.Page = fixture.Store.Page with { Entries = [ProviderHistoryQueryTestContext.Entry()] };
        fixture.Store.BeforeReturn = _ => {
            if (profile) {
                fixture.Authority.Context = fixture.Authority.Context with { Fence = new(2, 1) };
            } else {
                fixture.Authority.Permissions.Remove(HistoryPermission.ReadMetadata);
            }
            return Task.CompletedTask;
        };
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(fixture.Query, default));
        Assert.Equal(profile ? HistoryFailure.StaleContext : HistoryFailure.Denied, error.Failure);
    }

    [Theory]
    [InlineData(HistoryPermission.ReadMetadata)]
    [InlineData(HistoryPermission.ReadContent)]
    public async Task Metadata_and_content_require_separate_explicit_permissions(HistoryPermission missing) {
        using var fixture = new ProviderHistoryQueryTestContext();
        var entry = fixture.PrepareCanonical();
        fixture.Authority.Permissions.Remove(missing);
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() =>
            fixture.Service.GetDetailAsync(entry.Id, fixture.Owner.Mutation!.Source, default));
        Assert.Equal(HistoryFailure.Denied, error.Failure);
        Assert.Equal(0, fixture.Owner.ContentReads);
    }

    [Fact]
    public async Task Owner_permission_is_required_before_source_content_read() {
        using var fixture = new ProviderHistoryQueryTestContext();
        var entry = fixture.PrepareCanonical();
        fixture.Authority.OwnerDenied = true;
        await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.GetDetailAsync(entry.Id, fixture.Owner.Mutation!.Source, default));
        Assert.Equal(0, fixture.Owner.ContentReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_deletion_or_expiry_before_detail_publication_denies_content(bool expired) {
        using var fixture = new ProviderHistoryQueryTestContext();
        var entry = fixture.PrepareCanonical();
        if (expired) {
            fixture.Owner.Detail = fixture.Owner.Detail with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
        } else {
            fixture.Owner.AfterDetail = () => fixture.Owner.Mutation = null;
        }
        var detail = await fixture.Service.GetDetailAsync(entry.Id, fixture.Store.Metadata!.Owners[0].Source, default);
        Assert.Null(detail.Input);
        Assert.Null(detail.Response);
        Assert.Equal(expired ? HistoryDetailState.Expired : HistoryDetailState.Unavailable, detail.State);
    }

    [Fact]
    public async Task Canonical_content_is_read_only_after_explicit_owner_selection_and_rechecked() {
        using var fixture = new ProviderHistoryQueryTestContext();
        var entry = fixture.PrepareCanonical();
        var metadataOnly = await fixture.Service.GetMetadataAsync(entry.Id, default);
        Assert.Equal(0, fixture.Owner.ContentReads);
        var detail = await fixture.Service.GetDetailAsync(entry.Id, metadataOnly!.Owners[0].Source, default);
        Assert.Equal("private prompt", detail.Input!.Text);
        Assert.Equal(2, fixture.Authority.OwnerChecks);
        Assert.Equal(0, fixture.Store.ContentReads);
    }

    [Fact]
    public async Task Metadata_revocation_during_standalone_content_read_denies_publication() {
        using var fixture = new ProviderHistoryQueryTestContext();
        var entry = ProviderHistoryQueryTestContext.Entry();
        fixture.Store.Metadata = new(entry, []);
        fixture.Store.AfterDetail = () => fixture.Authority.Permissions.Remove(HistoryPermission.ReadMetadata);
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.GetDetailAsync(entry.Id, null, default));
        Assert.Equal(HistoryFailure.Denied, error.Failure);
    }

    [Fact]
    public async Task Backend_errors_are_sanitized_and_do_not_expose_payloads() {
        using var fixture = new ProviderHistoryQueryTestContext();
        fixture.Store.BeforeReturn = _ => throw new InvalidOperationException("private-provider-secret");
        var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(fixture.Query, default));
        Assert.Equal(HistoryFailure.Unavailable, error.Failure);
        Assert.DoesNotContain("private-provider-secret", error.ToString());
    }

    [Fact]
    public async Task Concurrent_read_limit_rejects_overload_without_queuing() {
        using var fixture = new ProviderHistoryQueryTestContext();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeReturn = token => release.Task.WaitAsync(token);
        var running = Enumerable.Range(0, 4).Select(_ => fixture.Service.SearchAsync(fixture.Query, default)).ToArray();
        try {
            var error = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Service.SearchAsync(fixture.Query, default));
            Assert.Equal(HistoryFailure.Unavailable, error.Failure);
            Assert.Equal(4, fixture.Store.Searches);
        } finally {
            release.TrySetResult();
            await Task.WhenAll(running);
        }
    }

    [Fact]
    public async Task Caller_cancellation_remains_cancellation() {
        using var fixture = new ProviderHistoryQueryTestContext();
        using var cancelled = new CancellationTokenSource();
        fixture.Store.BeforeReturn = token => Task.Delay(Timeout.Infinite, token);
        var running = fixture.Service.SearchAsync(fixture.Query, cancelled.Token);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }
}
