using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class SqliteWriteCoordinationIntegrationTests
{
    [Fact]
    public async Task Concurrent_activity_writes_complete_without_sqlite_lock_errors()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var activityService = scope.ServiceProvider.GetRequiredService<ActivityService>();
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var writeTasks = Enumerable.Range(0, 24)
            .Select(index => RecordActivityAsync(activityService, startSignal.Task, index))
            .ToArray();

        startSignal.SetResult();
        await Task.WhenAll(writeTasks);

        var activityItems = await activityService.ListRecentAsync(200);
        Assert.Equal(24, activityItems.Count(item => item.Category == "sqlite-coordination"));
    }

    [Fact]
    public async Task Concurrent_search_index_writes_complete_without_sqlite_lock_errors()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var writeTasks = Enumerable.Range(0, 24)
            .Select(index => UpsertSearchDocumentAsync(searchIndexService, startSignal.Task, index))
            .ToArray();

        startSignal.SetResult();
        await Task.WhenAll(writeTasks);

        var searchResults = await searchIndexService.SearchAsync("Concurrent sqlite coordination document", 50);
        Assert.Equal(24, searchResults.Count(item => item.Category == "SQLite coordination"));
    }

    private static async Task RecordActivityAsync(ActivityService activityService, Task startSignal, int index)
    {
        await startSignal;
        await activityService.RecordAsync(new ActivityWriteRequest(
            "sqlite-coordination",
            "write",
            $"Concurrent activity {index}",
            $"Activity write {index}",
            Route: $"/tests/sqlite/activity/{index}",
            Actor: "integration-tests"));
    }

    private static async Task UpsertSearchDocumentAsync(ISearchIndexService searchIndexService, Task startSignal, int index)
    {
        await startSignal;
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "sqlite-coordination",
            $"document-{index}",
            "SQLite coordination",
            $"Concurrent sqlite coordination document {index}",
            $"Concurrent sqlite coordination document summary {index}",
            $"Concurrent sqlite coordination document body {index}",
            $"/tests/sqlite/search/{index}"));
    }
}
