using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAcceptedExecutionInputMigrationTests {
    private const string Previous = "20261020131404_AddAcceptedProcessSourceProjection";
    private const string Migration = "20261021041654_AddAcceptedProcessExecutionInput";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_restart_and_uncaptured_downgrade_preserve_original_bytes_without_backfill(bool existingSource) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-input-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        if (existingSource) {
            var launch = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchScalingFixture.Create(1, 256));
            var source = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(launch));
            Accept(launch, source);
            context.Add(launch);
            await context.SaveChangesAsync();
            var assignment = source.Assignments.Single();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO process_accepted_assignment_sources ("AdmissionId", "StepInstanceId", "PayloadJson", "Fingerprint")
                VALUES ({launch.Id}, {assignment.Key.Value}, {assignment.Value.PayloadJson}, {assignment.Value.Fingerprint})
                """);
        }
        var before = await OriginalRowsAsync(context);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await reopened.GetService<IMigrator>().MigrateAsync(Migration);
            Assert.Equal(before, await OriginalRowsAsync(reopened));
            Assert.False(reopened.Database.HasPendingModelChanges());
            var sources = await reopened.Set<ProcessAcceptedAssignmentSourceEntity>().ToArrayAsync();
            Assert.Equal(existingSource ? 1 : 0, sources.Length);
            Assert.All(sources, item => {
                Assert.Equal(ProcessAcceptedExecutionInputVersion.NotCaptured, item.InputVersion);
                Assert.Null(item.InputJson);
                Assert.Null(item.InputFingerprint);
            });
        }
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(before, await OriginalRowsAsync(context));
        Assert.Equal(Previous, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Fact]
    public async Task Constraints_and_guarded_downgrade_preserve_captured_input_until_parent_cleanup() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("accepted-input-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        var launch = ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchScalingFixture.Create(1, 256));
        var source = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(launch));
        Accept(launch, source);
        context.Add(launch);
        var metadata = source.Assignments.Single();
        var domain = "process-accepted-execution-input/v1\n" + source.Header.Fingerprint + "\n" + metadata.Value.Fingerprint + "\n";
        var assignment = ProcessPreparedLaunchCodec.Read(launch).Preparation.InitialCommit.InitialAssignments!.Single();
        var input = ProcessAcceptedSourceCodec.Encode(assignment, domain, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes);
        context.Add(new ProcessAcceptedAssignmentSourceEntity {
            AdmissionId = launch.Id, StepInstanceId = metadata.Key.Value, PayloadJson = metadata.Value.PayloadJson,
            Fingerprint = metadata.Value.Fingerprint, InputVersion = ProcessAcceptedExecutionInputVersion.OriginalAssignment,
            InputJson = input.PayloadJson, InputFingerprint = input.Fingerprint
        });
        await context.SaveChangesAsync();
        var before = await AllRowsAsync(context);
        foreach (var invalid in new[] {
            """UPDATE process_accepted_assignment_sources SET "InputVersion" = 2""",
            """UPDATE process_accepted_assignment_sources SET "InputVersion" = 0""",
            """UPDATE process_accepted_assignment_sources SET "InputJson" = NULL""",
            """UPDATE process_accepted_assignment_sources SET "InputFingerprint" = NULL""",
            """UPDATE process_accepted_assignment_sources SET "InputFingerprint" = 'invalid'""",
            """UPDATE process_accepted_assignment_sources SET "InputJson" = repeat('x', 4194305)"""
        }) {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(before, await AllRowsAsync(context));
        }
        var denied = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Equal(PostgresErrorCodes.RaiseException, denied.SqlState);
        Assert.Contains("retained original Process execution input", denied.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, await AllRowsAsync(context));
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
        await reopened.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(before, await AllRowsAsync(reopened));
        await reopened.Set<ProcessPreparedLaunchEntity>().Where(item => item.Id == launch.Id).ExecuteDeleteAsync();
        Assert.Empty(await reopened.Set<ProcessAcceptedAssignmentSourceEntity>().ToArrayAsync());
        await reopened.GetService<IMigrator>().MigrateAsync(Previous);
    }

    private static void Accept(ProcessPreparedLaunchEntity launch, ProcessAcceptedSourceProjection source) {
        launch.AcceptedAtUtc = launch.PreparedAtUtc;
        launch.AcceptedSourceVersion = ProcessAcceptedSourceVersion.OriginalProjection;
        launch.AcceptedSourceHeaderJson = source.Header.PayloadJson;
        launch.AcceptedSourceHeaderFingerprint = source.Header.Fingerprint;
    }

    private static Task<string[]> OriginalRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(p)::text AS "Value" FROM process_prepared_launches p
        UNION ALL SELECT (to_jsonb(s) - 'InputVersion' - 'InputJson' - 'InputFingerprint')::text
        FROM process_accepted_assignment_sources s ORDER BY "Value"
        """).ToArrayAsync();

    private static Task<string[]> AllRowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(p)::text AS "Value" FROM process_prepared_launches p
        UNION ALL SELECT to_jsonb(s)::text FROM process_accepted_assignment_sources s ORDER BY "Value"
        """).ToArrayAsync();
}
