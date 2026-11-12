using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiReadLifetimeTests {
    [Fact]
    public async Task Current_denial_then_repeated_page_retirement_releases_owned_read() {
        using var authority = new ApiViewLifetime();
        using var page = new ApiPageController<string>((_, _) => throw new UnauthorizedAccessException(), authority);
        await page.RefreshAsync();
        Assert.False(authority.IsActive);
        page.Dispose();
        page.Dispose();
        Assert.False(page.IsLoading);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_retirement_then_noncooperative_completion_releases_owned_read(bool denied) {
        using var authority = new ApiViewLifetime();
        var result = new TaskCompletionSource<ApiPage<string>>();
        using var page = new ApiPageController<string>((_, _) => result.Task, authority);
        var reading = page.RefreshAsync();
        authority.Dispose();
        if (denied) {
            result.SetException(new UnauthorizedAccessException());
        } else {
            result.SetResult(new(["retired"], 1));
        }
        await reading;
        page.Dispose();
        page.Dispose();
        Assert.Null(page.Page);
        Assert.False(page.IsLoading);
    }

    [Fact]
    public async Task Session_retirement_then_held_access_completion_allows_repeated_disposal() {
        var store = new ApiScenarioStore();
        var access = new TaskCompletionSource<bool>();
        using var session = new ApiAccessSession(new HeldAccess(await store.ReadAsync(default), access), store, store, new());
        var loading = session.LoadAsync();
        session.Dispose();
        access.SetResult(true);
        await loading;
        session.Dispose();
        session.Dispose();
        Assert.Null(session.Issuance);
        Assert.Null(session.Accounts);
    }

    [Fact]
    public async Task Old_finally_does_not_detach_successor_cancellation() {
        using var authority = new ApiViewLifetime();
        var first = new TaskCompletionSource<ApiPage<string>>();
        var second = new TaskCompletionSource<ApiPage<string>>();
        CancellationToken successorToken = default;
        var calls = 0;
        using var page = new ApiPageController<string>((_, token) => {
            if (++calls == 1) {
                return first.Task;
            }
            successorToken = token;
            return second.Task;
        }, authority);
        var old = page.RefreshAsync();
        var current = page.RefreshAsync();
        first.SetResult(new(["old"], 1));
        await old;
        Assert.True(page.IsLoading);
        Assert.False(successorToken.IsCancellationRequested);
        page.Dispose();
        Assert.True(successorToken.IsCancellationRequested);
        second.SetResult(new(["successor"], 1));
        await current;
        Assert.Null(page.Page);
    }

    [Theory]
    [InlineData(ApiScenarioOperation.AccountSearch)]
    [InlineData(ApiScenarioOperation.TokenSearch)]
    public async Task Current_rendered_list_denial_retires_sensitive_siblings_and_retry_is_fresh(ApiScenarioOperation operation) {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var cut = context.Render<ApiAccessSurface>(parameters => parameters.Add(component => component.Session, session));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        var issuance = session.Issuance!;
        Assert.NotNull(issuance.Disclosure);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-create]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-user-password]").Input("synthetic-sensitive-draft"));
        var accounts = session.Accounts!;
        var password = accounts.Editor!;
        store.FaultNext(operation, ApiScenarioFault.Denied);
        if (operation == ApiScenarioOperation.AccountSearch) {
            await cut.InvokeAsync(accounts.Page.RefreshAsync);
        } else {
            await cut.InvokeAsync(() => cut.Find("[data-testid=api-tokens-open]").ClickAsync(new()));
        }
        Assert.Equal(ApiFailure.Denied, session.ManagementFailure);
        Assert.Null(session.Issuance);
        Assert.Null(session.Accounts);
        Assert.Null(issuance.Disclosure);
        Assert.Empty(password.Password);
        cut.WaitForAssertion(() => {
            Assert.Empty(cut.FindAll("[data-testid=api-issued-token]"));
            Assert.Empty(cut.FindAll("[data-testid=api-user-password]"));
            Assert.NotEmpty(cut.FindAll("[data-testid=api-token-access-denied]"));
        });
        await issuance.IssueAsync();
        await accounts.Page.RefreshAsync();
        Assert.Equal(1, store.DurableWrites);
        await cut.InvokeAsync(() => cut.Find("[data-testid=api-access-retry]").ClickAsync(new()));
        Assert.Equal(ApiFailure.None, session.ManagementFailure);
        Assert.NotSame(issuance, session.Issuance);
        Assert.Null(session.Issuance!.Disclosure);
        Assert.Single(session.Receipts);
        session.Dispose();
        session.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_bounded_page_correction_denial_retires_shared_authority(bool tokenPage) {
        var store = new ApiScenarioStore(ApiScenario.MultiplePages);
        var reads = new CorrectedPages(store);
        using var session = new ApiAccessSession(store, reads, reads, new());
        await session.LoadAsync();
        var issuance = session.Issuance!;
        await issuance.IssueAsync();
        using var tokens = issuance.OpenTokens();
        await tokens.Page.RefreshAsync();
        reads.DenyCorrection = true;
        await (tokenPage ? tokens.Page.NextAsync() : session.Accounts!.Page.NextAsync());
        Assert.Equal([25, 0], reads.CorrectionOffsets);
        Assert.Equal(ApiFailure.Denied, session.ManagementFailure);
        Assert.Null(issuance.Disclosure);
        Assert.Null(session.Accounts);
        await session.LoadAsync();
        Assert.NotNull(session.Issuance);
        Assert.Null(session.Issuance.Disclosure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_or_prior_activation_token_denial_cannot_retire_successor(bool reactivate) {
        var store = new ApiScenarioStore();
        var reads = new CorrectedPages(store);
        using var session = new ApiAccessSession(store, reads, reads, new());
        await session.LoadAsync();
        var issuance = session.Issuance!;
        await issuance.IssueAsync();
        using var tokens = issuance.OpenTokens();
        reads.Held = new();
        var loading = tokens.Page.RefreshAsync();
        if (reactivate) {
            await session.LoadAsync();
        } else {
            tokens.Dispose();
            Assert.Same(issuance, session.Issuance);
            Assert.NotNull(issuance.Disclosure);
        }
        var successor = session.Issuance;
        reads.Held.SetException(new UnauthorizedAccessException());
        await loading;
        Assert.Same(successor, session.Issuance);
        Assert.Equal(ApiFailure.None, session.ManagementFailure);
        Assert.True(successor!.CanIssue);
    }

    private sealed class CorrectedPages(ApiScenarioStore store) : IApiTokenOwner, IApiAccountOwner {
        public bool DenyCorrection { get; set; }
        public List<int> CorrectionOffsets { get; } = [];
        public TaskCompletionSource<ApiPage<ApiTokenMetadata>>? Held { get; set; }
        private bool Correct(ApiPageQuery query) {
            if (!DenyCorrection) {
                return false;
            }
            CorrectionOffsets.Add(query.Offset);
            if (query.Offset == 0) {
                DenyCorrection = false;
                throw new UnauthorizedAccessException();
            }
            return true;
        }
        Task<ApiPage<ApiTokenMetadata>> IApiTokenOwner.SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) =>
            Held?.Task ?? (Correct(query) ? Task.FromResult(new ApiPage<ApiTokenMetadata>([], 1)) : ((IApiTokenOwner)store).SearchAsync(query, cancellationToken));
        Task<ApiPage<ApiAccountMetadata>> IApiAccountOwner.SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) =>
            Correct(query) ? Task.FromResult(new ApiPage<ApiAccountMetadata>([], 1)) : ((IApiAccountOwner)store).SearchAsync(query, cancellationToken);
        public Task<ApiTokenDisclosure> IssueAsync(ApiTokenIntent intent, CancellationToken cancellationToken) => store.IssueAsync(intent, cancellationToken);
        public Task<ApiWriteResult> ApplyAsync(ApiTokenAction action, CancellationToken cancellationToken) => store.ApplyAsync(action, cancellationToken);
        public Task<ApiAccountWriteResult> ApplyAsync(ApiAccountIntent intent, CancellationToken cancellationToken) => store.ApplyAsync(intent, cancellationToken);
        Task<ApiTokenMetadata?> IApiTokenOwner.ObserveAsync(Guid id, CancellationToken cancellationToken) => ((IApiTokenOwner)store).ObserveAsync(id, cancellationToken);
        Task<ApiAccountMetadata?> IApiAccountOwner.ObserveAsync(Guid id, CancellationToken cancellationToken) => ((IApiAccountOwner)store).ObserveAsync(id, cancellationToken);
    }

    private sealed class HeldAccess(ApiAccessConfiguration configuration, TaskCompletionSource<bool> access) : IApiAccessConfigurationOwner {
        public Task<ApiAccessConfiguration> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken) => new(access.Task);
    }
}
