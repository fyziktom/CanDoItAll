using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiPageTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_B_A_reads_fence_old_success_error_and_finally(bool failOld) {
        using var authority = new ApiViewLifetime();
        var reads = new Queue<TaskCompletionSource<ApiPage<string>>>();
        using var controller = new ApiPageController<string>((_, _) => {
            var source = new TaskCompletionSource<ApiPage<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            reads.Enqueue(source);
            return source.Task;
        }, authority);
        controller.DesiredSearch = "A";
        var first = controller.SearchAsync();
        var a = reads.Dequeue();
        controller.DesiredSearch = "B";
        var second = controller.SearchAsync();
        var b = reads.Dequeue();
        controller.DesiredSearch = "A";
        var third = controller.SearchAsync();
        var latest = reads.Dequeue();
        b.SetResult(new(["obsolete B"], 1));
        await second;
        if (failOld) {
            a.SetException(new IOException("late failure"));
        } else {
            a.SetResult(new(["obsolete A"], 1));
        }
        await first;
        Assert.True(controller.IsLoading);
        Assert.Null(controller.Page);
        Assert.Null(controller.Error);
        latest.SetResult(new(["current A"], 1));
        await third;
        Assert.Equal("current A", Assert.Single(controller.Page!.Items));
        Assert.Equal(new ApiPageQuery("A"), controller.AcceptedQuery);
        Assert.False(controller.IsLoading);
    }

    [Fact]
    public async Task Initial_failure_is_unavailable_and_retry_accepts_a_true_empty_page() {
        using var authority = new ApiViewLifetime();
        var fail = true;
        using var controller = new ApiPageController<string>((_, _) => fail
            ? throw new IOException("sentinel must not be exposed") : Task.FromResult(new ApiPage<string>([], 0)), authority);
        await controller.RefreshAsync();
        Assert.Null(controller.Page);
        Assert.NotNull(controller.Error);
        Assert.DoesNotContain("sentinel", controller.Error);
        fail = false;
        await controller.RefreshAsync();
        Assert.Empty(controller.Page!.Items);
        Assert.Null(controller.Error);
    }

    [Fact]
    public async Task Failed_new_query_retains_the_accepted_query_and_disables_its_pager() {
        using var authority = new ApiViewLifetime();
        using var controller = new ApiPageController<string>((query, _) => query.Search == "new"
            ? throw new IOException() : Task.FromResult(new ApiPage<string>(["original row"], 26)), authority);
        controller.DesiredSearch = "old";
        await controller.SearchAsync();
        Assert.True(controller.CanNext);
        controller.DesiredSearch = "new";
        await controller.SearchAsync();
        Assert.Equal("old", controller.AcceptedQuery!.Search);
        Assert.Equal("original row", Assert.Single(controller.Page!.Items));
        Assert.True(controller.IsStale);
        Assert.False(controller.CanNext);
        Assert.False(controller.CanPrevious);
    }

    [Fact]
    public async Task Page_correction_is_bounded_and_cannot_hijack_a_newer_query() {
        using var authority = new ApiViewLifetime();
        var calls = new List<ApiPageQuery>();
        var correction = new TaskCompletionSource<ApiPage<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new ApiPageController<string>((query, _) => {
            calls.Add(query);
            return calls.Count switch {
                1 => Task.FromResult(new ApiPage<string>(["first"], 26)),
                2 => Task.FromResult(new ApiPage<string>([], 1)),
                3 => correction.Task,
                _ => Task.FromResult(new ApiPage<string>(["new query"], 1))
            };
        }, authority);
        await controller.RefreshAsync();
        var paging = controller.NextAsync();
        Assert.Equal([0, 25, 0], calls.Select(query => query.Offset));
        controller.DesiredSearch = "new";
        await controller.SearchAsync();
        correction.SetResult(new(["old correction"], 1));
        await paging;
        Assert.Equal("new", controller.AcceptedQuery!.Search);
        Assert.Equal("new query", Assert.Single(controller.Page!.Items));
        Assert.Equal(4, calls.Count);
    }

    [Fact]
    public async Task Retired_read_cannot_change_retained_metadata_or_revoke_successor_authority() {
        using var authority = new ApiViewLifetime();
        var pending = new TaskCompletionSource<ApiPage<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new ApiPageController<string>((_, _) => pending.Task, authority);
        var reading = controller.RefreshAsync();
        controller.Dispose();
        pending.SetException(new UnauthorizedAccessException());
        await reading;
        Assert.True(authority.IsActive);
        Assert.Null(controller.Page);
        Assert.False(controller.IsLoading);
    }
}
