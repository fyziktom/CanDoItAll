using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

public static class RuntimeHostServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllRuntimeModules(this IServiceCollection services)
    {
        services.AddSecurityModule();
        services.AddWorkspaceModule();
        services.AddProjectsModule();
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddFactoryModule();
        services.AddProcessesModule();
        services.AddValidationModule();
        services.AddTestLabModule();
        services.AddActivityModule();
        services.AddAgentFrameworkModule();
        services.AddAutomationModule();
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        return services;
    }

    public static IServiceCollection AddCanDoItAllRuntimeDatabaseSwitching(this IServiceCollection services)
    {
        services.AddSingleton<IAppDatabaseBootstrapper, AppDatabaseBootstrapper>();
        services.AddSingleton<IDatabaseSwitchCoordinator, DatabaseSwitchCoordinator>();
        return services;
    }
}

public sealed class AppDatabaseBootstrapper(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISwitchableAppDbContextFactory dbContextFactory,
    ILogger<AppDatabaseBootstrapper> logger) : IAppDatabaseBootstrapper
{
    private static readonly Guid ManagedDeliveryUnitPartyId = Guid.Parse("10BE49B1-EF4D-4A58-B9EA-B3F7D40F31A1");
    private static readonly Guid ManagedProductOwnerPartyId = Guid.Parse("A6BBAD2B-9D18-40EA-95B5-6D73C20C3078");
    private static readonly Guid ManagedDeliveryManagerPartyId = Guid.Parse("4B4718D5-4F86-4A6A-9BE7-3ACCA7E0F2AB");
    private static readonly Guid ManagedDeliveryUnitRoleId = Guid.Parse("1A8A7BB6-10B5-4D18-A91F-00F25E045DBF");
    private static readonly Guid ManagedProductOwnerRoleId = Guid.Parse("DBF3B8E6-77D2-49D5-924A-74CA8FFFBFD3");
    private static readonly Guid ManagedDeliveryManagerRoleId = Guid.Parse("2D9DF6AC-8B49-43EA-960E-8B912A758296");
    private static readonly Guid ManagedProductOwnerProfileId = Guid.Parse("61C29FAE-C560-4C2D-993E-BE842FD635FB");
    private static readonly Guid ManagedDeliveryManagerProfileId = Guid.Parse("E0EBEC09-C37B-4F42-9FA4-1B2DDAC20572");

    public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProfileReadyAsync(profileAccessor.ResolveCurrentProfile(), cancellationToken);
    }

    public async Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await CanDoItAllDatabaseMigrationBootstrap.PrepareLegacySqliteAsync(dbContext, logger, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await CrmHrSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await EnsureManagedSqliteStaffingBootstrapAsync(profile, dbContext, cancellationToken);
    }

    private async Task EnsureManagedSqliteStaffingBootstrapAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (profile.Profile.SourceKind != DatabaseProfileSourceKind.ManagedSqlite)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var parties = await dbContext.Set<Party>()
            .Where(item =>
                item.Id == ManagedDeliveryUnitPartyId ||
                item.Id == ManagedProductOwnerPartyId ||
                item.Id == ManagedDeliveryManagerPartyId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item =>
                item.Id == ManagedDeliveryUnitRoleId ||
                item.Id == ManagedProductOwnerRoleId ||
                item.Id == ManagedDeliveryManagerRoleId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var profiles = await dbContext.Set<WorkforceProfile>()
            .Where(item =>
                item.Id == ManagedProductOwnerProfileId ||
                item.Id == ManagedDeliveryManagerProfileId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var changed = false;

        if (!parties.ContainsKey(ManagedDeliveryUnitPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedDeliveryUnitPartyId,
                PartyType = PartyType.OrganizationUnit,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Managed Demo Delivery Leadership",
                LegalName = "Managed Demo Delivery Leadership",
                PreferredName = "Managed Demo Delivery Leadership",
                ExternalCode = "managed-sqlite-demo-delivery-unit",
                Summary = "Bootstrap delivery unit for managed SQLite staffing and process-start review flows.",
                Notes = "Created automatically so process launch review has factual CRM-HR delivery coverage in managed SQLite profiles.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"delivery-unit\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!parties.ContainsKey(ManagedProductOwnerPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedProductOwnerPartyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Parker Product",
                LegalName = "Parker Product",
                PreferredName = "Parker",
                ExternalCode = "managed-sqlite-demo-product-owner",
                Summary = "Managed SQLite bootstrap product owner used for staffing suggestions and process-launch validation.",
                Notes = "Created automatically so product-owner process roles can be matched from CRM-HR without guesswork.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"product-owner\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!parties.ContainsKey(ManagedDeliveryManagerPartyId))
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = ManagedDeliveryManagerPartyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Devon Delivery",
                LegalName = "Devon Delivery",
                PreferredName = "Devon",
                ExternalCode = "managed-sqlite-demo-delivery-manager",
                Summary = "Managed SQLite bootstrap delivery manager used for staffing suggestions and process-launch validation.",
                Notes = "Created automatically so delivery-manager process roles can be matched from CRM-HR without guesswork.",
                TagsJson = "[\"managed-sqlite\",\"demo\",\"delivery-manager\"]",
                Region = "Remote",
                CountryCode = "US",
                TimeZone = "America/La_Paz",
                ExtendedDataJson = "{}",
                LastChangedBy = "managed-sqlite-bootstrap",
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedDeliveryUnitRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedDeliveryUnitRoleId,
                PartyId = ManagedDeliveryUnitPartyId,
                RoleKind = PartyRoleKind.DeliveryUnit,
                Title = "Delivery leadership",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap delivery unit role."
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedProductOwnerRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedProductOwnerRoleId,
                PartyId = ManagedProductOwnerPartyId,
                RoleKind = PartyRoleKind.Employee,
                Title = "Product owner",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap workforce role."
            });
            changed = true;
        }

        if (!roles.ContainsKey(ManagedDeliveryManagerRoleId))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                Id = ManagedDeliveryManagerRoleId,
                PartyId = ManagedDeliveryManagerPartyId,
                RoleKind = PartyRoleKind.Employee,
                Title = "Delivery manager",
                IsPrimary = true,
                Notes = "Managed SQLite bootstrap workforce role."
            });
            changed = true;
        }

        if (!profiles.ContainsKey(ManagedProductOwnerProfileId))
        {
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                Id = ManagedProductOwnerProfileId,
                PartyId = ManagedProductOwnerPartyId,
                WorkforceKind = WorkforceKind.Employee,
                EmployeeCode = "MS-PO-001",
                JobTitle = "Product owner",
                Discipline = "Product management",
                Seniority = "Lead",
                HomeUnitPartyId = ManagedDeliveryUnitPartyId,
                Location = "Remote",
                TimeZone = "America/La_Paz",
                CapacityHoursPerWeek = 40m,
                Status = "Active",
                ExtendedDataJson = "{}",
                Notes = "Managed SQLite bootstrap workforce record for process-start staffing review."
            });
            changed = true;
        }

        if (!profiles.ContainsKey(ManagedDeliveryManagerProfileId))
        {
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                Id = ManagedDeliveryManagerProfileId,
                PartyId = ManagedDeliveryManagerPartyId,
                WorkforceKind = WorkforceKind.Employee,
                EmployeeCode = "MS-DM-001",
                JobTitle = "Delivery manager",
                Discipline = "Program delivery",
                Seniority = "Lead",
                HomeUnitPartyId = ManagedDeliveryUnitPartyId,
                Location = "Remote",
                TimeZone = "America/La_Paz",
                CapacityHoursPerWeek = 40m,
                Status = "Active",
                ExtendedDataJson = "{}",
                Notes = "Managed SQLite bootstrap workforce record for process-start staffing review."
            });
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded managed SQLite staffing bootstrap data for profile {ProfileId}.",
            profile.Profile.Id);
    }
}

public sealed class DatabaseSwitchCoordinator(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
    IDatabaseDriverRegistry driverRegistry,
    IDatabaseRuntimeState runtimeState,
    IAppDatabaseBootstrapper bootstrapper,
    ILogger<DatabaseSwitchCoordinator> logger) : IDatabaseSwitchCoordinator
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(15);

    public async Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default)
    {
        var currentProfile = profileAccessor.ResolveCurrentProfile();
        if (currentProfile.Profile.Runtime.LockedByRuntimeOverride)
        {
            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Runtime override is active. Database switching is disabled."));
        }

        if (currentProfile.Profile.Id == targetProfileId)
        {
            var snapshot = runtimeState.GetSnapshot();
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                currentProfile.Profile.Id,
                snapshot.Generation,
                Environment.ProcessId));
        }

        ResolvedDatabaseProfile targetProfile;
        try
        {
            targetProfile = profileAccessor.ResolveProfile(targetProfileId);
        }
        catch (Exception ex)
        {
            return Result<DatabaseSwitchResult>.Failure(Error.Failure(ex.Message));
        }

        await using var switchSession = await runtimeState.BeginSwitchAsync(cancellationToken);

        try
        {
            await switchSession.WaitForDrainAsync(DrainTimeout, cancellationToken);
            await driverRegistry.Resolve(targetProfile.Profile.ProviderKind)
                .EnsureDatabaseAsync(targetProfile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);

            var activationResult = await profileService.ActivateAsync(targetProfileId, cancellationToken);
            if (activationResult.IsFailure)
            {
                return Result<DatabaseSwitchResult>.Failure(activationResult.Errors);
            }

            var notification = switchSession.Complete(targetProfile);
            logger.LogInformation(
                "Switched active database from {PreviousProfileId} to {CurrentProfileId} at generation {Generation}.",
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation);

            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation,
                Environment.ProcessId));
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} timed out while waiting for active contexts to drain.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Database switch timed out while waiting for active operations to finish."));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} failed.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure($"Database switch failed: {ex.Message}"));
        }
    }
}
