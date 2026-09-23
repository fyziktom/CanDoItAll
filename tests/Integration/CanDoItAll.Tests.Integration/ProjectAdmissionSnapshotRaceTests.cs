using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectAdmissionSnapshotRaceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Final_admission_rejects_a_snapshot_established_while_waiting_for_retirement_and_recreation(bool bulkRead) {
        await using var application = await TestApplication.CreateAsync();
        await using var services = application.Services.CreateAsyncScope();
        var factory = services.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>();
        var canonical = services.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>();
        var coordinator = services.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>();
        var probe = new AdvisoryWaitProbe();
        var projectOptions = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(projectOptions, canonical.Profile);
        projectOptions.AddInterceptors(probe);
        var admissions = new ProjectWriteAdmissionService(factory, projectOptions.Options, coordinator, canonical);
        var project = new Project { Name = "Original lifetime" };
        await using var deletion = await factory.CreateDbContextAsync();
        deletion.Add(project);
        await deletion.SaveChangesAsync();
        var admission = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(project.Id));
        var projectKey = ProjectMutationScopeKeys.ForProject(project.Id);
        await using var deletionTransaction = await deletion.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(deletion, [projectKey], CancellationToken.None);
        var options = new DbContextOptionsBuilder<ResourcesDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, canonical.Profile);
        options.AddInterceptors(probe);
        await using var waiter = new ResourcesDbContext(options.Options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var attemptedSave = SaveWithOriginalAdmissionAsync();
        var waiterPid = await probe.Started.Task.WaitAsync(timeout.Token);
        await using var observer = await factory.CreateDbContextAsync(timeout.Token);
        await WaitForBlockedAdvisoryLockAsync(observer, waiterPid, timeout.Token);

        deletion.Add(new ProjectRetirementRecord {
            LifetimeId = project.LifetimeId, ProjectId = project.Id, RetiredAtUtc = DateTimeOffset.UtcNow
        });
        deletion.Remove(project);
        await deletion.SaveChangesAsync(timeout.Token);
        var replacement = new Project { Id = project.Id, Name = "Replacement lifetime" };
        deletion.Add(replacement);
        await deletion.SaveChangesAsync(timeout.Token);
        await deletionTransaction.CommitAsync(timeout.Token);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => attemptedSave);
        var postgres = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.SerializationFailure, postgres.SqlState);
        Assert.True(probe.FinalProjectLockRequested);
        await using var resources = await services.ServiceProvider.GetRequiredService<IDbContextFactory<ResourcesDbContext>>()
            .CreateDbContextAsync(timeout.Token);
        Assert.False(await resources.Set<ProjectResource>().AnyAsync(resource => resource.ProjectId == project.Id, timeout.Token));
        Assert.Equal(replacement.LifetimeId, (await observer.Set<Project>().AsNoTracking()
            .SingleAsync(row => row.Id == project.Id, timeout.Token)).LifetimeId);
        Assert.NotEqual(admission.LifetimeId, (await admissions.CaptureAsync(project.Id, timeout.Token))!.LifetimeId);

        async Task SaveWithOriginalAdmissionAsync() {
            await using var mutation = await SerializableMutationScope.BeginAsync(waiter, projectKey, timeout.Token);
            using (coordinator.Enter(waiter)) {
                if (bulkRead) {
                    await admissions.RequireManyForMutationAsync([admission], timeout.Token);
                } else {
                    await admissions.RequireForMutationAsync(admission, timeout.Token);
                }
                waiter.Add(new ProjectResource { ProjectId = project.Id, Name = "Forbidden late resource" });
                await waiter.SaveChangesAsync(timeout.Token);
            }
            await mutation.CommitAsync(timeout.Token);
        }
    }

    private static async Task WaitForBlockedAdvisoryLockAsync(ProjectsDbContext observer, int backendPid, CancellationToken cancellationToken) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            var waiting = await observer.Database.SqlQuery<int>($"""
                SELECT count(*)::integer AS "Value" FROM pg_locks
                WHERE pid = {backendPid} AND locktype = 'advisory' AND NOT granted
                """).SingleAsync(cancellationToken);
            if (waiting > 0) {
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }

    private sealed class AdvisoryWaitProbe : DbCommandInterceptor {
        public TaskCompletionSource<int> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FinalProjectLockRequested { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) && command.Connection is NpgsqlConnection connection) {
                Started.TrySetResult(connection.ProcessID);
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("FOR KEY SHARE", StringComparison.Ordinal)) {
                FinalProjectLockRequested = true;
            }
            return ValueTask.FromResult(result);
        }
    }
}
