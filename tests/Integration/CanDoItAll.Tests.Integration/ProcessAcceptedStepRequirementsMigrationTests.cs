using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAcceptedStepRequirementsMigrationTests {
    private const string Previous = "20261021041654_AddAcceptedProcessExecutionInput";

    [Theory]
    [InlineData(ProcessAcceptedExecutionInputVersion.NotCaptured)]
    [InlineData(ProcessAcceptedExecutionInputVersion.OriginalAssignment)]
    public async Task Upgrade_restart_and_legacy_downgrade_preserve_original_bytes(ProcessAcceptedExecutionInputVersion version) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-requirements-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, version);
        var before = await RowsAsync(context);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await reopened.GetService<IMigrator>().MigrateAsync(Migration(reopened));
            Assert.Equal(before, await RowsAsync(reopened));
            Assert.True(await ValidatedAsync(reopened));
            Assert.False(reopened.Database.HasPendingModelChanges());
        }
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(before, await RowsAsync(context));
    }

    [Fact]
    public async Task New_input_constraints_and_guarded_downgrade_preserve_requirements() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-requirements-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Migration(context));
        var launchId = await SeedAsync(context, ProcessAcceptedExecutionInputVersion.OriginalStepRequirements);
        var before = await RowsAsync(context);
        foreach (var invalid in new[] {
            """UPDATE process_accepted_assignment_sources SET "InputVersion" = 3""",
            """UPDATE process_accepted_assignment_sources SET "InputVersion" = 0""",
            """UPDATE process_accepted_assignment_sources SET "InputJson" = NULL""",
            """UPDATE process_accepted_assignment_sources SET "InputFingerprint" = NULL""",
            """UPDATE process_accepted_assignment_sources SET "InputFingerprint" = 'invalid'""",
            """UPDATE process_accepted_assignment_sources SET "InputJson" = repeat('x', 4194305)"""
        }) {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(before, await RowsAsync(context));
        }
        var denied = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Equal(PostgresErrorCodes.RaiseException, denied.SqlState);
        Assert.Contains("retained original Process step requirements", denied.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, await RowsAsync(context));
        Assert.True(await ValidatedAsync(context));
        Assert.Equal(Migration(context), (await context.Database.GetAppliedMigrationsAsync()).Last());
        await context.Set<ProcessPreparedLaunchEntity>().Where(item => item.Id == launchId).ExecuteDeleteAsync();
        Assert.Empty(await context.Set<ProcessAcceptedAssignmentSourceEntity>().ToArrayAsync());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
    }

    [Fact]
    public async Task Interrupted_validation_leaves_an_enforced_constraint_and_restart_completes_it() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-requirements-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(context, ProcessAcceptedExecutionInputVersion.OriginalAssignment);
        var before = await RowsAsync(context);
        var gate = new ValidationGate(() => throw new IOException("Interrupted before separate constraint validation."));
        var options = new DbContextOptionsBuilder<AppDbContext>(lease.CreateAppDbContextOptions()).AddInterceptors(gate).Options;
        await using (var interrupted = new AppDbContext(options)) {
            await Assert.ThrowsAsync<IOException>(() => interrupted.GetService<IMigrator>().MigrateAsync(Migration(interrupted)));
        }
        Assert.False(await ValidatedAsync(context));
        Assert.Equal(Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(before, await RowsAsync(context));
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            """UPDATE process_accepted_assignment_sources SET "InputVersion" = 3"""));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await SeedAsync(context, ProcessAcceptedExecutionInputVersion.OriginalStepRequirements);
        var widened = await RowsAsync(context);
        await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
        await reopened.GetService<IMigrator>().MigrateAsync(Migration(reopened));
        Assert.True(await ValidatedAsync(reopened));
        Assert.Equal(widened, await RowsAsync(reopened));
    }

    [Fact]
    public async Task Validation_finishes_while_another_transaction_holds_a_row_write_lock() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-requirements-migration");
        await using var setup = new AppDbContext(lease.CreateAppDbContextOptions());
        await setup.GetService<IMigrator>().MigrateAsync(Previous);
        await SeedAsync(setup, ProcessAcceptedExecutionInputVersion.OriginalAssignment);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new ValidationGate(async () => {
            entered.TrySetResult();
            await resume.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });
        var options = new DbContextOptionsBuilder<AppDbContext>(lease.CreateAppDbContextOptions()).AddInterceptors(gate).Options;
        await using var migrating = new AppDbContext(options);
        var migration = migrating.GetService<IMigrator>().MigrateAsync(Migration(migrating));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await using var writer = new AppDbContext(lease.CreateAppDbContextOptions());
            writer.Database.SetCommandTimeout(5);
            await using var transaction = await writer.Database.BeginTransactionAsync();
            await writer.Database.ExecuteSqlRawAsync("""UPDATE process_accepted_assignment_sources SET "Fingerprint" = "Fingerprint" """);
            resume.TrySetResult();
            await migration.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(await ValidatedAsync(migrating));
            await transaction.RollbackAsync();
        } finally {
            resume.TrySetResult();
            await migration.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    private static async Task<Guid> SeedAsync(AppDbContext context, ProcessAcceptedExecutionInputVersion version) {
        var preparation = ProcessPreparedLaunchScalingFixture.Create(1, 256);
        var launch = ProcessPreparedLaunchCodec.ToEntity(preparation);
        var source = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(launch));
        launch.AcceptedAtUtc = launch.PreparedAtUtc;
        launch.AcceptedSourceVersion = ProcessAcceptedSourceVersion.OriginalProjection;
        launch.AcceptedSourceHeaderJson = source.Header.PayloadJson;
        launch.AcceptedSourceHeaderFingerprint = source.Header.Fingerprint;
        var metadata = source.Assignments.Single();
        var domain = "process-accepted-execution-input/v1\n" + source.Header.Fingerprint + "\n" + metadata.Value.Fingerprint + "\n";
        var input = version switch {
            ProcessAcceptedExecutionInputVersion.NotCaptured => null,
            ProcessAcceptedExecutionInputVersion.OriginalAssignment => ProcessAcceptedSourceCodec.Encode(
                preparation.InitialCommit.InitialAssignments!.Single(), domain, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes),
            _ => source.ExecutionInputs[metadata.Key]
        };
        context.Add(launch);
        context.Add(new ProcessAcceptedAssignmentSourceEntity {
            AdmissionId = launch.Id, StepInstanceId = metadata.Key.Value, PayloadJson = metadata.Value.PayloadJson,
            Fingerprint = metadata.Value.Fingerprint, InputVersion = version, InputJson = input?.PayloadJson, InputFingerprint = input?.Fingerprint
        });
        await context.SaveChangesAsync();
        return launch.Id;
    }

    private static string Migration(AppDbContext context)
        => Assert.Single(context.Database.GetMigrations(), item => item.EndsWith("_AddAcceptedProcessStepRequirements", StringComparison.Ordinal));

    private static Task<bool> ValidatedAsync(AppDbContext context) => context.Database.SqlQueryRaw<bool>("""
        SELECT convalidated AS "Value" FROM pg_constraint
        WHERE conname = 'CK_process_accepted_execution_input' AND conrelid = 'process_accepted_assignment_sources'::regclass
        """).SingleAsync();

    private static Task<string[]> RowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(p)::text AS "Value" FROM process_prepared_launches p
        UNION ALL SELECT to_jsonb(s)::text FROM process_accepted_assignment_sources s ORDER BY "Value"
        """).ToArrayAsync();

    private sealed class ValidationGate(Func<Task> beforeValidation) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("VALIDATE CONSTRAINT \"CK_process_accepted_execution_input\"", StringComparison.Ordinal)) {
                await beforeValidation();
            }
            return result;
        }
    }
}
