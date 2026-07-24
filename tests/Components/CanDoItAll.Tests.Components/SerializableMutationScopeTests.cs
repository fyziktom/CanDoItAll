using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Components;

public sealed class SerializableMutationScopeTests
{
    [Fact]
    public async Task InMemory_multi_project_scope_serializes_both_project_keys()
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase($"serializable-scope-{Guid.NewGuid():N}")
            .Options;
        var sourceProjectId =
            Guid.Parse("10000000-0000-0000-0000-000000000021");
        var targetProjectId =
            Guid.Parse("20000000-0000-0000-0000-000000000022");
        var projectKeys = new[]
        {
            $"project:{sourceProjectId:D}",
            $"project:{targetProjectId:D}"
        };
        await using var ownerContext = new DbContext(options);
        var ownerScope = await SerializableMutationScope.BeginAsync(
            ownerContext,
            projectKeys,
            CancellationToken.None);
        try
        {
            foreach (var projectKey in projectKeys)
            {
                await using var contenderContext =
                    new DbContext(options);
                using var cancellationSource =
                    new CancellationTokenSource(
                        TimeSpan.FromMilliseconds(200));

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () =>
                    {
                        await using var blockedScope =
                            await SerializableMutationScope.BeginAsync(
                                contenderContext,
                                projectKey,
                                cancellationSource.Token);
                    });
            }
        }
        finally
        {
            await ownerScope.DisposeAsync();
        }

        foreach (var projectKey in projectKeys)
        {
            await using var context = new DbContext(options);
            using var cancellationSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var acquiredScope =
                await SerializableMutationScope.BeginAsync(
                    context,
                    projectKey,
                    cancellationSource.Token);
            await acquiredScope.CommitAsync(cancellationSource.Token);
        }
    }
}
