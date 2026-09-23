using CanDoItAll.Infrastructure.ControlPlane;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkAssignmentCoherencePersistenceTests {
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ReferenceOwner.Work)]
    [InlineData(ReferenceOwner.Participation)]
    [InlineData(ReferenceOwner.Staffing)]
    public async Task Raw_updates_cannot_store_an_empty_or_missing_bound_reference(ReferenceOwner owner) {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-coherence-reference");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("owner"));
        await MigrateAsync(services);
        await using var database = await CreateOwnerAsync(services, owner);
        var identity = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var lifetimeId = Guid.NewGuid();
        database.Add(Reference(owner, identity, projectId, lifetimeId));
        await database.SaveChangesAsync();
        var expected = await ReadRowsAsync(services);
        await AssertCheckAsync(() => UpdateReferenceAsync(database, owner, identity, projectId, Guid.Empty), Constraint(owner));
        await AssertCheckAsync(() => UpdateReferenceAsync(database, owner, identity, Guid.Empty, lifetimeId), Constraint(owner));
        if (owner == ReferenceOwner.Staffing) {
            await AssertCheckAsync(() => UpdateReferenceAsync(database, owner, identity, null, lifetimeId), Constraint(owner));
        }
        Assert.Equal(expected, await ReadRowsAsync(services));

        Assert.Equal(1, await UpdateReferenceAsync(database, owner, identity, Guid.Empty, null));
        Assert.Equal(1, await UpdateReferenceAsync(database, owner, identity, projectId, null));
        Assert.Equal(1, await UpdateReferenceAsync(database, owner, identity, projectId, lifetimeId));
        Assert.Equal(expected, await ReadRowsAsync(services));
    }

    [Fact]
    public async Task Move_receipts_accept_legacy_null_or_complete_nonempty_tuple_and_reject_every_other_shape() {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-coherence-move");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("owner"));
        await MigrateAsync(services);
        await using var database = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var receipt = Receipt();
        database.Add(receipt);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var legacy = await database.Set<ProjectPartyAssignmentMoveReceipt>().AsNoTracking().SingleAsync();
        Assert.Null(legacy.DatabaseProfileId);
        Assert.Null(legacy.SourceProjectLifetimeId);
        Assert.Null(legacy.TargetProjectLifetimeId);

        var profileId = Guid.NewGuid();
        var sourceLifetimeId = Guid.NewGuid();
        var targetLifetimeId = Guid.NewGuid();
        Assert.Equal(1, await UpdateMoveAsync(database, receipt.OperationId, profileId, sourceLifetimeId, targetLifetimeId));
        var expected = await ReadRowsAsync(services);
        Guid?[] values = [null, Guid.Empty, Guid.NewGuid()];
        var rejected = 0;
        foreach (var profile in values) {
            foreach (var source in values) {
                foreach (var target in values) {
                    if (profile is null && source is null && target is null ||
                        profile is { } first && first != Guid.Empty && source is { } second && second != Guid.Empty &&
                        target is { } third && third != Guid.Empty) {
                        continue;
                    }
                    await AssertCheckAsync(() => UpdateMoveAsync(database, receipt.OperationId, profile, source, target),
                        "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes");
                    Assert.Equal(expected, await ReadRowsAsync(services));
                    rejected++;
                }
            }
        }
        Assert.Equal(25, rejected);
        await AssertCheckAsync(() => UpdateMoveProjectsAsync(database, receipt.OperationId, Guid.Empty, receipt.TargetProjectId),
            "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes");
        await AssertCheckAsync(() => UpdateMoveProjectsAsync(database, receipt.OperationId, receipt.SourceProjectId, Guid.Empty),
            "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes");
        Assert.Equal(expected, await ReadRowsAsync(services));
        Assert.Equal(1, await UpdateMoveAsync(database, receipt.OperationId, null, null, null));
        var restored = await database.Set<ProjectPartyAssignmentMoveReceipt>().AsNoTracking().SingleAsync();
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(restored));
        Assert.Equal(1, await UpdateMoveProjectsAsync(database, receipt.OperationId, Guid.Empty, Guid.Empty));
        Assert.Equal(1, await UpdateMoveProjectsAsync(database, receipt.OperationId, receipt.SourceProjectId, receipt.TargetProjectId));
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(
            await database.Set<ProjectPartyAssignmentMoveReceipt>().AsNoTracking().SingleAsync()));
    }

    [Fact]
    public async Task Legacy_global_orphan_and_retired_references_remain_intact_after_recreation_and_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-coherence-retention");
        var profile = environment.CreatePostgreSqlProfile("original");
        string[] expected;
        Guid projectId;
        Guid originalLifetime;
        await using (var services = CreateProvider(profile)) {
            await MigrateAsync(services);
            await using var database = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            var project = new Project { Name = "Original project", Slug = Guid.NewGuid().ToString("N") };
            database.Add(project);
            await database.SaveChangesAsync();
            projectId = project.Id;
            originalLifetime = project.LifetimeId;
            foreach (var owner in Enum.GetValues<ReferenceOwner>()) {
                database.Add(Reference(owner, Guid.NewGuid(), projectId, originalLifetime));
                database.Add(Reference(owner, Guid.NewGuid(), Guid.NewGuid(), null));
            }
            database.Add(Reference(ReferenceOwner.Staffing, Guid.NewGuid(), null, null));
            database.Add(Receipt());
            var bound = Receipt();
            bound.DatabaseProfileId = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
            bound.SourceProjectLifetimeId = originalLifetime;
            bound.TargetProjectLifetimeId = Guid.NewGuid();
            database.Add(bound);
            await database.SaveChangesAsync();
            expected = await ReadRowsAsync(services);
            Assert.Equal(9, expected.Length);

            await using var transaction = await database.Database.BeginTransactionAsync();
            database.Remove(project);
            database.Add(new ProjectRetirementRecord { ProjectId = projectId, LifetimeId = originalLifetime, RetiredAtUtc = SavedAt });
            await database.SaveChangesAsync();
            database.Add(new Project { Id = projectId, Name = "Recreated project", Slug = Guid.NewGuid().ToString("N") });
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            Assert.Equal(expected, await ReadRowsAsync(services));
        }
        await using var restarted = CreateProvider(profile);
        await MigrateAsync(restarted);
        Assert.Equal(expected, await ReadRowsAsync(restarted));
        await using var current = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.NotEqual(originalLifetime, (await current.Set<Project>().SingleAsync(row => row.Id == projectId)).LifetimeId);
        Assert.Equal(originalLifetime, (await current.Set<ProjectWorkAssignmentRecord>().SingleAsync(row => row.ProjectId == projectId)).ProjectLifetimeId);
        Assert.Equal(originalLifetime, (await current.Set<ProjectPartyAssignment>().SingleAsync(row => row.ProjectId == projectId)).ProjectLifetimeId);
        Assert.Equal(originalLifetime, (await current.Set<StaffingRequest>().SingleAsync(row => row.ProjectId == projectId)).ProjectLifetimeId);
        Assert.Null((await current.Set<StaffingRequest>().SingleAsync(row => row.ProjectId == null)).ProjectLifetimeId);
    }

    [Fact]
    public async Task Owner_and_canonical_models_keep_the_same_checks_and_existing_affiliation_constraints() {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-coherence-model");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("owner"));
        await MigrateAsync(services);
        await using var canonical = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var work = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        await using var crm = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        AssertCheckParity<ProjectWorkAssignmentRecord>(work, canonical, Constraint(ReferenceOwner.Work));
        AssertCheckParity<ProjectPartyAssignment>(crm, canonical, Constraint(ReferenceOwner.Participation));
        AssertCheckParity<StaffingRequest>(crm, canonical, Constraint(ReferenceOwner.Staffing));
        AssertCheckParity<ProjectPartyAssignmentMoveReceipt>(crm, canonical, "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes");
        var model = canonical.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(ProjectWorkAssignmentRecord), typeof(ProjectPartyAssignment) }) {
            var entity = model.FindEntityType(type)!;
            var affiliation = Assert.Single(entity.GetForeignKeys(), key => key.Properties.Any(property => property.Name == "PartyOrganizationAffiliationId"));
            Assert.Equal(typeof(PartyOrganizationAffiliation), affiliation.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Restrict, affiliation.DeleteBehavior);
            Assert.DoesNotContain(entity.GetForeignKeys(), key => key.Properties.Any(property => property.Name == "ProjectLifetimeId"));
        }
        Assert.Contains(model.FindEntityType(typeof(ProjectPartyAssignment))!.GetCheckConstraints(),
            check => check.Name == "CK_CrmHr_ProjectPartyAssignments_ParticipationRole");
    }

    private static void AssertCheckParity<TEntity>(DbContext owner, AppDbContext canonical, string name) {
        var owned = owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))!;
        var complete = canonical.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))!;
        Assert.Equal(Assert.Single(owned.GetCheckConstraints(), check => check.Name == name).Sql,
            Assert.Single(complete.GetCheckConstraints(), check => check.Name == name).Sql);
    }

    private static object Reference(ReferenceOwner owner, Guid identity, Guid? projectId, Guid? lifetimeId) => owner switch {
        ReferenceOwner.Work => new ProjectWorkAssignmentRecord { Id = identity, ProjectId = projectId!.Value, ProjectLifetimeId = lifetimeId,
            PartyId = Guid.NewGuid(), NodeKey = "task:historical", PhaseName = "Preserved phase", AllocationPercent = 12.123456789m,
            StartsAtUtc = SavedAt, EndsAtUtc = SavedAt.AddDays(3), Source = "Preserved source", Notes = "Original notes\nwith trailing spaces  " },
        ReferenceOwner.Participation => new ProjectPartyAssignment { Id = identity, ProjectId = projectId!.Value, ProjectLifetimeId = lifetimeId,
            PartyId = Guid.NewGuid(), AssignmentKind = ProjectPartyAssignmentKind.Manager, NodeKey = "task:historical",
            PhaseName = "Preserved phase", AllocationPercent = 24.123456789m, StartsAtUtc = SavedAt, EndsAtUtc = SavedAt.AddDays(3),
            Source = "Preserved participation", Notes = "Original CRM notes\nwith trailing spaces  " },
        ReferenceOwner.Staffing => new StaffingRequest { Id = identity, ProjectId = projectId, ProjectLifetimeId = lifetimeId,
            Title = "Retained staffing", NeededRole = "Preserved role", NeededSkillsJson = "[\"original skill\"]",
            AllocationPercent = 31.125m, StartDateUtc = SavedAt, EndDateUtc = SavedAt.AddDays(3), Notes = "Preserved staffing notes  " },
        _ => throw new ArgumentOutOfRangeException(nameof(owner))
    };

    private static ProjectPartyAssignmentMoveReceipt Receipt() => new() {
        OperationId = Guid.NewGuid(), SourceProjectId = Guid.NewGuid(), TargetProjectId = Guid.NewGuid(),
        NodeSetFingerprint = new string('a', 64), CompletedAtUtc = SavedAt
    };

    private static string Constraint(ReferenceOwner owner) => owner switch {
        ReferenceOwner.Work => "CK_Workbench_WorkAssignments_ProjectLifetime",
        ReferenceOwner.Participation => "CK_CrmHr_ProjectPartyAssignments_ProjectLifetime",
        ReferenceOwner.Staffing => "CK_CrmHr_StaffingRequests_ProjectLifetime",
        _ => throw new ArgumentOutOfRangeException(nameof(owner))
    };

    private static Task<int> UpdateReferenceAsync(DbContext database, ReferenceOwner owner, Guid id, Guid? projectId, Guid? lifetimeId) => owner switch {
        ReferenceOwner.Work => database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Workbench_WorkAssignments" SET "ProjectId" = {projectId}, "ProjectLifetimeId" = {lifetimeId} WHERE "Id" = {id}
            """),
        ReferenceOwner.Participation => database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CrmHr_ProjectPartyAssignments" SET "ProjectId" = {projectId}, "ProjectLifetimeId" = {lifetimeId} WHERE "Id" = {id}
            """),
        ReferenceOwner.Staffing => database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CrmHr_StaffingRequests" SET "ProjectId" = {projectId}, "ProjectLifetimeId" = {lifetimeId} WHERE "Id" = {id}
            """),
        _ => throw new ArgumentOutOfRangeException(nameof(owner))
    };

    private static Task<int> UpdateMoveAsync(CrmHrDbContext database, Guid id, Guid? profile, Guid? source, Guid? target)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CrmHr_ProjectPartyAssignmentMoveReceipts"
            SET "DatabaseProfileId" = {profile}, "SourceProjectLifetimeId" = {source}, "TargetProjectLifetimeId" = {target}
            WHERE "OperationId" = {id}
            """);

    private static Task<int> UpdateMoveProjectsAsync(CrmHrDbContext database, Guid id, Guid source, Guid target)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "CrmHr_ProjectPartyAssignmentMoveReceipts"
            SET "SourceProjectId" = {source}, "TargetProjectId" = {target} WHERE "OperationId" = {id}
            """);

    private static async Task AssertCheckAsync(Func<Task<int>> update, string constraint) {
        var failure = await Assert.ThrowsAsync<PostgresException>(update);
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    private static async Task<DbContext> CreateOwnerAsync(ServiceProvider services, ReferenceOwner owner) {
        if (owner == ReferenceOwner.Work) {
            return await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        }
        return await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
    }

    private static async Task MigrateAsync(ServiceProvider services) {
        await using var database = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await database.Database.MigrateAsync();
    }

    private static async Task<string[]> ReadRowsAsync(ServiceProvider services) {
        await using var database = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        return await database.Database.SqlQueryRaw<string>("""
            SELECT 'work:' || to_jsonb(saved)::text AS "Value" FROM "Workbench_WorkAssignments" saved
            UNION ALL SELECT 'participation:' || to_jsonb(saved)::text AS "Value" FROM "CrmHr_ProjectPartyAssignments" saved
            UNION ALL SELECT 'staffing:' || to_jsonb(saved)::text AS "Value" FROM "CrmHr_StaffingRequests" saved
            UNION ALL SELECT 'move:' || to_jsonb(saved)::text AS "Value" FROM "CrmHr_ProjectPartyAssignmentMoveReceipts" saved
            ORDER BY "Value"
            """).ToArrayAsync();
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public enum ReferenceOwner { Work, Participation, Staffing }
}
