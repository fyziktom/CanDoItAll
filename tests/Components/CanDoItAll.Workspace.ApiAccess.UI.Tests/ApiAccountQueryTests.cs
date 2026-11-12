using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiAccountQueryTests {
    [Fact]
    public async Task A_routed_query_opens_the_list_and_operator_search_and_paging_are_requested() {
        var store = new ApiScenarioStore(ApiScenario.MultiplePages);
        using var session = new ApiAccessSession(store, store, store, new());
        var accepted = Observe(session);

        await session.OpenAccountsAsync(new("fixture", 50));
        await session.LoadAsync();
        var page = session.Accounts!.Page;
        Assert.Equal(new ApiPageQuery("fixture", 50), page.AcceptedQuery);
        Assert.Equal("fixture", page.DesiredSearch);
        Assert.Equal(11, page.Page!.Items.Length);

        await page.PreviousAsync();
        page.DesiredSearch = "fixture-1";
        await page.SearchAsync();
        await page.RefreshAsync();

        Assert.Equal([
            new ApiPageQueryAcceptance(new("fixture", 50), ApiPageQueryCause.Owner),
            new ApiPageQueryAcceptance(new("fixture", 25), ApiPageQueryCause.Operator),
            new ApiPageQueryAcceptance(new("fixture-1", 0), ApiPageQueryCause.Operator),
            new ApiPageQueryAcceptance(new("fixture-1", 0), ApiPageQueryCause.Owner)
        ], accepted);
        Assert.Equal(new ApiPageQuery("fixture-1", 0), session.AccountQuery);
    }

    [Fact]
    public async Task An_out_of_range_routed_page_is_corrected_by_the_owner_and_survives_a_retry() {
        var store = new ApiScenarioStore(ApiScenario.MultiplePages);
        using var session = new ApiAccessSession(store, store, store, new());
        var accepted = Observe(session);
        await session.LoadAsync();

        await session.OpenAccountsAsync(new(string.Empty, 2500));
        await session.LoadAsync();

        Assert.Equal(new ApiPageQuery(string.Empty, 50), session.AccountQuery);
        Assert.Equal(new ApiPageQuery(string.Empty, 50), session.Accounts!.Page.AcceptedQuery);
        Assert.Equal([
            new ApiPageQueryAcceptance(new(string.Empty, 0), ApiPageQueryCause.Owner),
            new ApiPageQueryAcceptance(new(string.Empty, 50), ApiPageQueryCause.Owner),
            new ApiPageQueryAcceptance(new(string.Empty, 50), ApiPageQueryCause.Owner)
        ], accepted);
    }

    [Fact]
    public async Task A_failed_read_is_not_accepted_and_retains_the_previous_query() {
        var store = new ApiScenarioStore(ApiScenario.MultiplePages);
        using var session = new ApiAccessSession(store, store, store, new());
        var accepted = Observe(session);
        await session.LoadAsync();
        store.FaultNext(ApiScenarioOperation.AccountSearch, ApiScenarioFault.Unavailable);

        await session.Accounts!.Page.NextAsync();

        Assert.Single(accepted);
        Assert.Equal(new ApiPageQuery(string.Empty, 0), session.AccountQuery);
        Assert.NotNull(session.Accounts.Page.Error);
    }

    private static List<ApiPageQueryAcceptance> Observe(ApiAccessSession session) {
        var accepted = new List<ApiPageQueryAcceptance>();
        session.AccountQueryAccepted += accepted.Add;
        return accepted;
    }
}
