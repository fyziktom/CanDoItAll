using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class MigrationBootstrapIntegrationTests
{
    private const string InitialPostgreSqlBaselineMigrationId = "20260528182412_InitialPostgreSqlBaseline";
    private const string WorkflowCheckpointsMigrationId = "20260529111314_AddWorkflowCheckpoints";
    private const string SchedulerRunObservabilityMigrationId = "20260529220032_AddSchedulerRunObservability";
    private const string DisableCognitiveMemoryByDefaultMigrationId = "20260603113251_DisableCognitiveMemoryByDefault";
    private const string ProcessVerificationAuditRecordsMigrationId = "20260610113813_AddProcessVerificationAuditRecords";
    private const string SubprocessChildArtifactMappingMigrationId = "20260612173521_PersistSubprocessChildArtifactMapping";
    private const string WorkspaceCurrencySettingsMigrationId = "20260612222259_AddWorkspaceCurrencySettings";
    private const string ProcessModuleArchitectureV3RuntimePersistenceMigrationId = "20260615235147_ProcessModuleArchitectureV3RuntimePersistence";
    private const string ProcessV3RuntimeTablesMigrationId = "20260616144322_ProcessV3RuntimeTables";
    private const string ProcessRuntimeAssignmentOperationContractsMigrationId = "20260616155920_ProcessRuntimeAssignmentOperationContracts";
    private const string ProcessRuntimeAssignmentLaunchVariablesMigrationId = "20260616162335_ProcessRuntimeAssignmentLaunchVariables";
    private const string ProcessRuntimeEventGlobalSequenceIdentityRepairMigrationId = "20260617131500_ProcessRuntimeEventGlobalSequenceIdentityRepair";
    private const string ProcessRuntimeAssignmentRoleIdentityMigrationId = "20260618103000_ProcessRuntimeAssignmentRoleIdentity";
    private const string RemoveUnusedValidationActivityAutomationModulesMigrationId = "20260621212712_RemoveUnusedValidationActivityAutomationModules";
    private const string GenericMemoryProviderRuntimeMigrationId = "20260705163628_GenericMemoryProviderRuntime";
    private const string RetireLegacyCognitiveMemoryMainDbModelMigrationId = "20260706015654_RetireLegacyCognitiveMemoryMainDbModel";
    private const string IncludeCognitiveMemoryModuleModelMigrationId = "20260707110549_IncludeCognitiveMemoryModuleModel";
    private const string ProcessRuntimeAssignmentCapabilityScopeMigrationId = "20260707134848_ProcessRuntimeAssignmentCapabilityScope";
    private const string ProcessStrategyResultReceiptLineageMigrationId = "20260707195705_ProcessStrategyResultReceiptLineage";
    private const string ProcessRuntimeInputArtifactContractsMigrationId = "20260707222506_ProcessRuntimeInputArtifactContracts";
    private const string ProcessRuntimeStepArtifactDescriptorsMigrationId = "20260708120721_ProcessRuntimeStepArtifactDescriptors";
    private const string DistributedMemoryWorkerPhaseLeasesMigrationId = "20260712133000_DistributedMemoryWorkerPhaseLeases";
    private const string RetireNativeCognitiveMemoryModelMetadataMigrationId = "20260712133717_RetireNativeCognitiveMemoryModelMetadata";
    private const string WorkflowUsageAnalyticsMigrationId = "20260712204230_AddWorkflowUsageAnalytics";
    private const string ProcessWorkflowExecutorBindingMigrationId = "20260712210953_AddProcessWorkflowExecutorBinding";
    private const string WorkflowLaunchIdempotencyMigrationId = "20260712215655_AddWorkflowLaunchIdempotency";
    private static readonly string[] ExpectedPostgreSqlMigrations =
    [
        InitialPostgreSqlBaselineMigrationId,
        WorkflowCheckpointsMigrationId,
        SchedulerRunObservabilityMigrationId,
        DisableCognitiveMemoryByDefaultMigrationId,
        ProcessVerificationAuditRecordsMigrationId,
        SubprocessChildArtifactMappingMigrationId,
        WorkspaceCurrencySettingsMigrationId,
        ProcessModuleArchitectureV3RuntimePersistenceMigrationId,
        ProcessV3RuntimeTablesMigrationId,
        ProcessRuntimeAssignmentOperationContractsMigrationId,
        ProcessRuntimeAssignmentLaunchVariablesMigrationId,
        ProcessRuntimeEventGlobalSequenceIdentityRepairMigrationId,
        ProcessRuntimeAssignmentRoleIdentityMigrationId,
        RemoveUnusedValidationActivityAutomationModulesMigrationId,
        GenericMemoryProviderRuntimeMigrationId,
        RetireLegacyCognitiveMemoryMainDbModelMigrationId,
        IncludeCognitiveMemoryModuleModelMigrationId,
        ProcessRuntimeAssignmentCapabilityScopeMigrationId,
        ProcessStrategyResultReceiptLineageMigrationId,
        ProcessRuntimeInputArtifactContractsMigrationId,
        ProcessRuntimeStepArtifactDescriptorsMigrationId,
        DistributedMemoryWorkerPhaseLeasesMigrationId,
        RetireNativeCognitiveMemoryModelMetadataMigrationId,
        WorkflowUsageAnalyticsMigrationId,
        ProcessWorkflowExecutorBindingMigrationId,
        WorkflowLaunchIdempotencyMigrationId
    ];

    [Fact]
    public async Task Bootstrap_migrates_a_new_postgresql_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migrations");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var resolver = scope.ServiceProvider.GetRequiredService<IActiveDatabaseProfileResolver>();

        var profile = resolver.ResolveCurrentProfile();

        Assert.Equal(DatabaseProviderKind.PostgreSql, profile.Profile.ProviderKind);
        Assert.Equal(activeProfile.ConnectionString, profile.ConnectionString);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Contains(InitialPostgreSqlBaselineMigrationId, await dbContext.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_for_postgresql_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migration-idempotent");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-idempotent");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Bootstrap_adopts_existing_postgresql_schema_without_migration_history()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-existing-schema");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-existing-schema");

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var existingSchemaContext = await dbContextFactory.CreateDbContextAsync())
        {
            await existingSchemaContext.Database.EnsureCreatedAsync();
            Assert.Empty(await existingSchemaContext.Database.GetAppliedMigrationsAsync());
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Equal(ExpectedPostgreSqlMigrations, appliedMigrations);
        Assert.True(await dbContext.Database.CanConnectAsync());
    }
}
