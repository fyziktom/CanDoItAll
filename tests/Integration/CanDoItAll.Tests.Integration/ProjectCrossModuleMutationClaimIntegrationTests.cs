using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectCrossModuleMutationClaimIntegrationTests
{
    private static readonly ProjectCrossModuleMutationProcessingOptions ProcessingOptions = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromSeconds(5));

    [Fact]
    public async Task Lost_failure_claim_returns_the_winners_authoritative_payload()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var mutationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string rootNodeKey = "winner-payload-proof";
        var authoritativePayload = JsonSerializer.Serialize(
            new DeleteSubtreeMutationPayload(
                rootNodeKey,
                [rootNodeKey],
                0,
                ManagedStorageDisposition:
                    ProjectStructureManagedStorageDisposition.RetainManagedFiles));
        var timestamp = DateTimeOffset.UtcNow;
        await using (var seedContext = await dbContextFactory.CreateDbContextAsync())
        {
            seedContext.Set<ProjectCrossModuleMutationRecord>().Add(
                new ProjectCrossModuleMutationRecord
                {
                    Id = mutationId,
                    ProjectId = projectId,
                    ScopeNodeKey = rootNodeKey,
                    MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree,
                    Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted,
                    ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
                    PayloadJson = "{ invalid-loser-payload",
                    CreatedAtUtc = timestamp,
                    UpdatedAtUtc = timestamp
                });
            await seedContext.SaveChangesAsync();
        }

        var winnerCompleted = false;
        var processor = CreateProcessor(
            scope.ServiceProvider,
            new SystemClock(),
            new CallbackLogger<ProjectCrossModuleMutationProcessor>(() =>
            {
                if (winnerCompleted)
                {
                    return;
                }

                winnerCompleted = true;
                using var winnerContext = dbContextFactory.CreateDbContext();
                winnerContext.Set<ProjectCrossModuleMutationRecord>()
                    .Where(record => record.Id == mutationId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(
                            record => record.Status,
                            ProjectCrossModuleMutationStatus.Completed)
                        .SetProperty(record => record.PayloadJson, authoritativePayload)
                        .SetProperty(record => record.ErrorMessage, string.Empty)
                        .SetProperty(record => record.CompletedAtUtc, timestamp)
                        .SetProperty(record => record.UpdatedAtUtc, timestamp));
            }));

        var result = await processor.ProcessWithPayloadAsync(mutationId);

        Assert.True(winnerCompleted);
        Assert.NotNull(result);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, result.Status);
        Assert.Equal(authoritativePayload, result.PayloadJson);
    }

    [Fact]
    public async Task PostgreSql_claim_uses_database_time_and_exactly_one_connection_takes_an_expired_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var farFutureClock = new FixedClock(
            new DateTimeOffset(2200, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var farPastClock = new FixedClock(
            new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var farFutureProcessor = CreateProcessor(scope.ServiceProvider, farFutureClock);
        var farPastProcessor = CreateProcessor(scope.ServiceProvider, farPastClock);
        var mutationId = Guid.NewGuid();
        var projectDeletionMutationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        DateTimeOffset databaseNow;
        await using (var seedContext = await dbContextFactory.CreateDbContextAsync())
        {
            databaseNow = await GetDatabaseUtcNowAsync(seedContext);
            seedContext.Set<ProjectCrossModuleMutationRecord>().AddRange(
                new ProjectCrossModuleMutationRecord
                {
                    Id = mutationId,
                    ProjectId = projectId,
                    ScopeNodeKey = "claim-clock-proof",
                    MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree,
                    Status = ProjectCrossModuleMutationStatus.Processing,
                    ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
                    PayloadJson = JsonSerializer.Serialize(
                        new DeleteSubtreeMutationPayload(
                            "claim-clock-proof",
                            [],
                            0)),
                    ErrorMessage = "existing-owner",
                    AttemptCount = 1,
                    LastAttemptAtUtc = databaseNow,
                    CreatedAtUtc = databaseNow,
                    UpdatedAtUtc = databaseNow
                },
                new ProjectCrossModuleMutationRecord
                {
                    Id = projectDeletionMutationId,
                    ProjectId = projectId,
                    ScopeNodeKey = "project",
                    MutationKind = ProjectCrossModuleMutationKind.DeleteProject,
                    Status = ProjectCrossModuleMutationStatus.Processing,
                    ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
                    PayloadJson = JsonSerializer.Serialize(
                        new DeleteProjectMutationPayload([], [])),
                    ErrorMessage = "existing-project-owner",
                    AttemptCount = 1,
                    LastAttemptAtUtc = databaseNow,
                    CreatedAtUtc = databaseNow,
                    UpdatedAtUtc = databaseNow
                });
            await seedContext.SaveChangesAsync();
        }

        var futureSubtreeRecovery = Assert.Single(
            await CreateMutationService(scope.ServiceProvider, farFutureClock)
                .ListPendingDeletionRecoveriesAsync(projectId));
        var pastSubtreeRecovery = Assert.Single(
            await CreateMutationService(scope.ServiceProvider, farPastClock)
                .ListPendingDeletionRecoveriesAsync(projectId));
        Assert.Equal(futureSubtreeRecovery.CanRetryNow, pastSubtreeRecovery.CanRetryNow);
        Assert.False(futureSubtreeRecovery.CanRetryNow);
        Assert.Equal(
            databaseNow + ProcessingOptions.LeaseDuration,
            futureSubtreeRecovery.RetryAvailableAtUtc);
        Assert.Equal(
            futureSubtreeRecovery.RetryAvailableAtUtc,
            pastSubtreeRecovery.RetryAvailableAtUtc);

        var futureProjectRecovery = Assert.Single(
            await CreateDeletionParticipant(scope.ServiceProvider, farFutureClock)
                .ListPendingRecoveriesAsync());
        var pastProjectRecovery = Assert.Single(
            await CreateDeletionParticipant(scope.ServiceProvider, farPastClock)
                .ListPendingRecoveriesAsync());
        Assert.Equal(futureProjectRecovery.CanRetryNow, pastProjectRecovery.CanRetryNow);
        Assert.False(futureProjectRecovery.CanRetryNow);
        Assert.Equal(
            databaseNow + ProcessingOptions.LeaseDuration,
            futureProjectRecovery.RetryAvailableAtUtc);
        Assert.Equal(
            futureProjectRecovery.RetryAvailableAtUtc,
            pastProjectRecovery.RetryAvailableAtUtc);

        await using (var freshClaimContext = await dbContextFactory.CreateDbContextAsync())
        {
            Assert.False(await farFutureProcessor.TryClaimAsync(
                freshClaimContext,
                mutationId,
                "future-clock-owner",
                CancellationToken.None));
        }

        await using (var expireContext = await dbContextFactory.CreateDbContextAsync())
        {
            await expireContext.Set<ProjectCrossModuleMutationRecord>()
                .Where(record => record.Id == mutationId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        record => record.LastAttemptAtUtc,
                        databaseNow - ProcessingOptions.LeaseDuration - TimeSpan.FromMinutes(1))
                    .SetProperty(
                        record => record.UpdatedAtUtc,
                        databaseNow - ProcessingOptions.LeaseDuration - TimeSpan.FromMinutes(1)));
        }

        await using var firstClaimContext = await dbContextFactory.CreateDbContextAsync();
        await using var secondClaimContext = await dbContextFactory.CreateDbContextAsync();
        var claimWindowStartedAt = await GetDatabaseUtcNowAsync(firstClaimContext);
        var releaseClaims = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClaim = ClaimWhenReleasedAsync(
            farFutureProcessor,
            firstClaimContext,
            mutationId,
            "future-clock-owner",
            releaseClaims.Task);
        var secondClaim = ClaimWhenReleasedAsync(
            farPastProcessor,
            secondClaimContext,
            mutationId,
            "past-clock-owner",
            releaseClaims.Task);

        releaseClaims.SetResult();
        var claims = await Task.WhenAll(firstClaim, secondClaim);

        Assert.Single(claims, claimed => claimed);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var claimWindowEndedAt = await GetDatabaseUtcNowAsync(verificationContext);
        var claimedMutation = await verificationContext
            .Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == mutationId);
        Assert.Equal(ProjectCrossModuleMutationStatus.Processing, claimedMutation.Status);
        Assert.Equal(2, claimedMutation.AttemptCount);
        Assert.Contains(
            claimedMutation.ErrorMessage,
            new[] { "future-clock-owner", "past-clock-owner" });
        Assert.NotNull(claimedMutation.LastAttemptAtUtc);
        Assert.InRange(
            claimedMutation.LastAttemptAtUtc.Value,
            claimWindowStartedAt - TimeSpan.FromSeconds(1),
            claimWindowEndedAt + TimeSpan.FromSeconds(1));
    }

    private static ProjectCrossModuleMutationProcessor CreateProcessor(
        IServiceProvider services,
        IClock clock)
        => CreateProcessor(
            services,
            clock,
            NullLogger<ProjectCrossModuleMutationProcessor>.Instance);

    private static ProjectCrossModuleMutationProcessor CreateProcessor(
        IServiceProvider services,
        IClock clock,
        ILogger<ProjectCrossModuleMutationProcessor> logger)
    {
        return new ProjectCrossModuleMutationProcessor(
            services.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            services.GetRequiredService<IProjectPartyIntegrationBridge>(),
            services.GetRequiredService<ProjectManagedStorageDeletionService>(),
            new ProjectCrossModuleMutationCoordinator(clock),
            clock,
            ProcessingOptions,
            TimeProvider.System,
            logger);
    }

    private static ProjectWorkbenchCrossModuleMutationService CreateMutationService(
        IServiceProvider services,
        IClock clock)
    {
        return new ProjectWorkbenchCrossModuleMutationService(
            services.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            clock,
            ProcessingOptions,
            new ProjectCrossModuleMutationCoordinator(clock),
            CreateProcessor(services, clock),
            services.GetRequiredService<ProjectManagedStorageDeletionPlanner>(),
            services.GetRequiredService<ProjectStructureAssemblyService>());
    }

    private static ProjectWorkbenchDeletionParticipant CreateDeletionParticipant(
        IServiceProvider services,
        IClock clock)
    {
        return new ProjectWorkbenchDeletionParticipant(
            services.GetRequiredService<ProjectManagedStorageDeletionPlanner>(),
            new ProjectCrossModuleMutationCoordinator(clock),
            CreateProcessor(services, clock),
            ProcessingOptions,
            clock,
            services.GetRequiredService<IDbContextFactory<AppDbContext>>());
    }

    private static async Task<bool> ClaimWhenReleasedAsync(
        ProjectCrossModuleMutationProcessor processor,
        AppDbContext dbContext,
        Guid mutationId,
        string claimToken,
        Task release)
    {
        await release;
        return await processor.TryClaimAsync(
            dbContext,
            mutationId,
            claimToken,
            CancellationToken.None);
    }

    private static Task<DateTimeOffset> GetDatabaseUtcNowAsync(AppDbContext dbContext)
    {
        return dbContext.Database
            .SqlQueryRaw<DateTimeOffset>(
                "SELECT CURRENT_TIMESTAMP AS \"Value\"")
            .SingleAsync();
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset GetUtcNow() => value;
    }

    private sealed class CallbackLogger<T>(Action onWarning) : ILogger<T>
    {
        private int callbackInvoked;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning &&
                Interlocked.Exchange(ref callbackInvoked, 1) == 0)
            {
                onWarning();
            }
        }
    }
}
