using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessPlanMigrationIntegrationTests
{
    private const string PreviousMigrationId = "20260811185352_AddProcessRuntimeStepHostCapabilities";
    private const string HashVersioningMigrationId = "20260812112732_AddProcessPlanHashVersioning";
    private const string CurrentMigrationId = "20260813012618_CorrectProcessPlanHashClassification";
    private const string LegacyHash = "sha256:8d4c8bb0aadf2b8a4ed5ef249457e5354789fec5872dd7749a4a51b9810938b6";
    private const string CurrentShapeHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PartialShapeHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly Guid PlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CurrentShapePlanId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PartialShapePlanId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task Process_plan_hash_classification_correction_is_transactional_idempotent_restart_safe_and_reversible()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-process-plan-migration");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("process-plan-migration");
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            activeProfile,
            new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath
            });
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using (var migrationContext = await dbContextFactory.CreateDbContextAsync())
        {
            var migrator = migrationContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigrationId);
            await migrationContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO process_instance_plans
                     ("PlanId", "RootPlanId", "ParentPlanId", "ParentStepId", "DefinitionId",
                      "DefinitionVersionId", "PlanHash", "PlanSchemaVersion", "DefinitionContentHash",
                      "PayloadJson", "CreatedAtUtc")
                 VALUES
                     ({PlanId}, {PlanId}, NULL, NULL,
                      {Guid.Parse("22222222-2222-2222-2222-222222222222")},
                      {Guid.Parse("33333333-3333-3333-3333-333333333333")},
                      {LegacyHash}, 'processes.instance-plan.v1', 'sha256:legacy-definition',
                      {LegacyPayload}, {new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero)}),
                     ({CurrentShapePlanId}, {CurrentShapePlanId}, NULL, NULL,
                      {Guid.Parse("66666666-6666-6666-6666-666666666666")},
                      {Guid.Parse("77777777-7777-7777-7777-777777777777")},
                      {CurrentShapeHash}, 'processes.instance-plan.v1', 'sha256:current-shape-definition',
                      {CurrentShapePayload}, {new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero)}),
                     ({PartialShapePlanId}, {PartialShapePlanId}, NULL, NULL,
                      {Guid.Parse("88888888-8888-8888-8888-888888888888")},
                      {Guid.Parse("99999999-9999-9999-9999-999999999999")},
                      {PartialShapeHash}, 'processes.instance-plan.v1', 'sha256:partial-shape-definition',
                      {PartialShapePayload}, {new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero)});
                 """);

            await migrator.MigrateAsync(HashVersioningMigrationId);
            var preCorrection = await migrationContext.Set<ProcessInstancePlanEntity>()
                .AsNoTracking()
                .ToDictionaryAsync(plan => plan.PlanId);
            Assert.Null(preCorrection[PlanId].PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.Unknown, preCorrection[PlanId].ExecutionState);
            Assert.Equal(ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2, preCorrection[CurrentShapePlanId].PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.Executable, preCorrection[CurrentShapePlanId].ExecutionState);
            Assert.Null(preCorrection[PartialShapePlanId].PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.Unknown, preCorrection[PartialShapePlanId].ExecutionState);

            await migrator.MigrateAsync(CurrentMigrationId);
            await migrator.MigrateAsync(CurrentMigrationId);
        }

        await using (var restartedContext = await dbContextFactory.CreateDbContextAsync())
        {
            var entity = await restartedContext.Set<ProcessInstancePlanEntity>()
                .AsNoTracking()
                .SingleAsync(plan => plan.PlanId == PlanId);
            Assert.Equal(ProcessPlanHashAlgorithmVersion.LegacyV1, entity.PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.NeedsRecompile, entity.ExecutionState);
            Assert.Equal(ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed, entity.MigrationReason);
            Assert.Equal(LegacyHash, entity.PlanHash);
            Assert.Equal(LegacyPayload, entity.PayloadJson);

            var currentShape = await restartedContext.Set<ProcessInstancePlanEntity>()
                .AsNoTracking()
                .SingleAsync(plan => plan.PlanId == CurrentShapePlanId);
            Assert.Equal(ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2, currentShape.PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.Executable, currentShape.ExecutionState);
            Assert.Null(currentShape.MigrationReason);
            Assert.Equal(CurrentShapeHash, currentShape.PlanHash);
            Assert.Equal(CurrentShapePayload, currentShape.PayloadJson);

            var partialShape = await restartedContext.Set<ProcessInstancePlanEntity>()
                .AsNoTracking()
                .SingleAsync(plan => plan.PlanId == PartialShapePlanId);
            Assert.Null(partialShape.PlanHashAlgorithmVersion);
            Assert.Equal(PersistedProcessPlanExecutionState.Unknown, partialShape.ExecutionState);
            Assert.Null(partialShape.MigrationReason);
            Assert.Equal(PartialShapeHash, partialShape.PlanHash);
            Assert.Equal(PartialShapePayload, partialShape.PayloadJson);

            var processDbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
            var store = new EfProcessInstancePlanStore(processDbContext);
            var exception = await Assert.ThrowsAsync<ProcessPlanMigrationRequiredException>(() =>
                store.LoadAsync(new ProcessInstancePlanId(PlanId)).AsTask());
            Assert.Equal(ProcessPlanMigrationReason.HostCapabilitiesWereNotSealed, exception.Reason);
        }

        await using (var rollbackContext = await dbContextFactory.CreateDbContextAsync())
        {
            await rollbackContext.Database.GetService<IMigrator>().MigrateAsync(PreviousMigrationId);
            var connection = rollbackContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "PlanHash", "PayloadJson"
                FROM process_instance_plans
                WHERE "PlanId" = '11111111-1111-1111-1111-111111111111';
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(LegacyHash, reader.GetString(0));
            Assert.Equal(LegacyPayload, reader.GetString(1));
            Assert.False(await reader.ReadAsync());
        }
    }

    private const string LegacyPayload = """
        {
          "header": {
            "planId": { "value": "11111111-1111-1111-1111-111111111111" },
            "rootPlanId": { "value": "11111111-1111-1111-1111-111111111111" },
            "parentPlanId": null,
            "parentStepId": null,
            "planSchemaVersion": "processes.instance-plan.v1",
            "createdAtUtc": "2026-08-01T00:00:00+00:00",
            "hierarchyDepth": 0
          },
          "definition": {
            "definitionId": { "value": "22222222-2222-2222-2222-222222222222" },
            "versionId": { "value": "33333333-3333-3333-3333-333333333333" },
            "definitionContentHash": "sha256:legacy-definition",
            "sourceSchemaVersion": "runtime/1.0",
            "targetSchemaVersion": "runtime/1.0",
            "appliedMigrationIds": [],
            "templateComponents": [],
            "appliedLocalOverridePointers": []
          },
          "driverStack": { "drivers": [] },
          "strategies": {
            "executionBindings": [],
            "managerBindings": [],
            "recoveryBindings": [],
            "resupplyBindings": []
          },
          "steps": [],
          "artifactPlan": { "slots": [], "initialLedgerEntries": [] },
          "branches": { "routes": [] },
          "subprocesses": [],
          "manager": {
            "policyHash": "sha256:legacy-manager",
            "managerStrategyBinding": null,
            "recoveryBindings": [],
            "resupplyBindings": []
          },
          "budgets": { "loopBudgets": [] },
          "monitoring": {
            "enabled": false,
            "projectionConfigHash": "sha256:legacy-projection"
          },
          "security": {
            "governancePolicyHash": "sha256:legacy-governance",
            "requiredApprovalKeys": []
          },
          "planHash": "sha256:8d4c8bb0aadf2b8a4ed5ef249457e5354789fec5872dd7749a4a51b9810938b6"
        }
        """;

    private const string CurrentShapePayload = """
        {
          "driverStack": {
            "drivers": [
              { "requiredHostCapabilities": [] }
            ],
            "hostProfileId": { "value": "linux-headless" },
            "hostCapabilities": []
          },
          "strategies": {
            "executionBindings": [],
            "managerBindings": [],
            "recoveryBindings": [],
            "resupplyBindings": []
          },
          "steps": [
            {
              "executionStrategyBinding": null,
              "requiredHostCapabilities": [],
              "requiredRuntimeToolNames": []
            }
          ],
          "manager": {
            "managerStrategyBinding": null,
            "recoveryBindings": [],
            "resupplyBindings": []
          }
        }
        """;

    private const string PartialShapePayload = """
        {
          "driverStack": {
            "drivers": [
              { "requiredHostCapabilities": [] }
            ],
            "hostProfileId": { "value": "linux-headless" },
            "hostCapabilities": []
          },
          "strategies": {
            "executionBindings": [],
            "managerBindings": [],
            "recoveryBindings": [],
            "resupplyBindings": []
          },
          "steps": [
            {
              "executionStrategyBinding": null,
              "requiredHostCapabilities": []
            }
          ],
          "manager": {
            "managerStrategyBinding": null,
            "recoveryBindings": [],
            "resupplyBindings": []
          }
        }
        """;
}
