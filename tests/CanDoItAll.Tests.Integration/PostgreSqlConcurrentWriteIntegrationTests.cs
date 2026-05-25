using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PostgreSqlConcurrentWriteIntegrationTests
{
    [Fact]
    public async Task Concurrent_activity_writes_complete()
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
        Assert.Equal(24, activityItems.Count(item => item.Category == "postgres-concurrency"));
    }

    [Fact]
    public async Task Concurrent_search_index_writes_complete()
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

        var searchResults = await searchIndexService.SearchAsync("Concurrent PostgreSQL concurrency document", 50);
        Assert.Equal(24, searchResults.Count(item => item.Category == "PostgreSQL concurrency"));
    }

    private static async Task RecordActivityAsync(ActivityService activityService, Task startSignal, int index)
    {
        await startSignal;
        await activityService.RecordAsync(new ActivityWriteRequest(
            "postgres-concurrency",
            "write",
            $"Concurrent activity {index}",
            $"Activity write {index}",
            Route: $"/tests/postgres/activity/{index}",
            Actor: "integration-tests"));
    }

    private static async Task UpsertSearchDocumentAsync(ISearchIndexService searchIndexService, Task startSignal, int index)
    {
        await startSignal;
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "postgres-concurrency",
            $"document-{index}",
            "PostgreSQL concurrency",
            $"Concurrent PostgreSQL concurrency document {index}",
            $"Concurrent PostgreSQL concurrency document summary {index}",
            $"Concurrent PostgreSQL concurrency document body {index}",
            $"/tests/postgres/search/{index}"));
    }
}
