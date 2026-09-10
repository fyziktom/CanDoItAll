using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class AiTechnicalProjectionPersistenceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Rename_and_archive_update_technical_projection_while_preserving_human_party_and_governance() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var partyId = Guid.NewGuid();
        var agent = Entry(partyId);
        await using (var seed = database.Factory.CreateDbContext()) {
            seed.Add(new Party {
                Id = partyId, PartyType = PartyType.AiAgent, DisplayName = "Human resource label", Summary = "Business summary",
                LifecycleStatus = PartyLifecycleStatus.Active, Notes = "Private governance", TagsJson = "[\"business\"]",
                LastChangedBy = "steward", CreatedAtUtc = Now.AddDays(-5), UpdatedAtUtc = Now.AddDays(-1)
            });
            seed.Add(new AiAgentProfile { PartyId = partyId, ValidationStatus = AiValidationStatus.Draft, Notes = "Review pending" });
            await seed.SaveChangesAsync();
        }

        await database.Store().ApplyAsync(database.Projection(1, agent));
        await database.Store().ApplyAsync(database.Projection(2, agent with {
            DisplayName = "Renamed technical Agent", Summary = "New technical summary", LifecycleStatus = AgentLifecycleStatus.Archived
        }));

        await using var read = database.Factory.CreateDbContext();
        var party = await read.Set<Party>().SingleAsync();
        Assert.Equal("Human resource label", party.DisplayName);
        Assert.Equal("Business summary", party.Summary);
        Assert.Equal(PartyLifecycleStatus.Active, party.LifecycleStatus);
        Assert.Equal("Private governance", party.Notes);
        Assert.Equal("[\"business\"]", party.TagsJson);
        Assert.Equal("steward", party.LastChangedBy);
        Assert.Equal(Now.AddDays(-1), party.UpdatedAtUtc);
        Assert.Equal("Review pending", (await read.Set<AiAgentProfile>().SingleAsync()).Notes);
        var projection = Assert.Single(await database.Store().ReadAsync([partyId])).Value;
        Assert.Equal("Human resource label", projection.Staffing.DisplayName);
        Assert.Equal("Renamed technical Agent", projection.Directory.Projection!.DisplayName);
        Assert.Equal(AgentLifecycleStatus.Archived, projection.Directory.Projection.LifecycleStatus);
        Assert.Equal(new CatalogDataRevision(2), projection.Directory.Projection.Revision);
    }

    [Fact]
    public async Task Newer_empty_catalog_rejects_delayed_older_nonempty_catalog() {
        await using var database = await ProjectionDatabase.CreateAsync();
        await database.Store().ApplyAsync(database.Projection(2));

        var delayed = await database.Store().ApplyAsync(database.Projection(1, Entry(Guid.NewGuid())));

        Assert.Equal(AiTechnicalProjectionApplyDisposition.Stale, delayed.Disposition);
        await using var read = database.Factory.CreateDbContext();
        Assert.Empty(await read.Set<Party>().ToListAsync());
        Assert.Empty(await read.Set<AiResourceBinding>().ToListAsync());
        Assert.Equal(2, (await read.Set<AiTechnicalProjectionCursor>().SingleAsync()).CatalogRevision);
    }

    [Fact]
    public async Task Equal_revision_with_different_semantic_content_is_rejected_without_rewriting() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var agent = Entry(Guid.NewGuid());
        await database.Store().ApplyAsync(database.Projection(1, agent));

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Store().ApplyAsync(database.Projection(1,
            agent with { Instructions = "Conflicting instructions" })));

        await using var read = database.Factory.CreateDbContext();
        Assert.Equal(agent.Instructions, (await read.Set<AiResourceBinding>().SingleAsync()).ProjectedInstructions);
        Assert.Equal(1, (await read.Set<AiTechnicalProjectionCursor>().SingleAsync()).CatalogRevision);
    }

    [Fact]
    public async Task Independent_instances_serialize_newer_empty_and_delayed_older_application() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var gate = new SaveGate();
        var newer = database.Store(gate).ApplyAsync(database.Projection(2));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var command = new CursorAttempt();
        var older = database.Store(command).ApplyAsync(database.Projection(1, Entry(Guid.NewGuid())));
        try {
            await command.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(older.IsCompleted);
        } finally {
            gate.Release.TrySetResult();
        }

        Assert.Equal(AiTechnicalProjectionApplyDisposition.Applied, (await newer).Disposition);
        Assert.Equal(AiTechnicalProjectionApplyDisposition.Stale, (await older).Disposition);
        await using var read = database.Factory.CreateDbContext();
        Assert.Empty(await read.Set<Party>().ToListAsync());
    }

    [Fact]
    public async Task Independent_instances_replaying_same_revision_create_one_binding() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var projection = database.Projection(1, Entry(Guid.NewGuid()));
        var result = await Task.WhenAll(database.Store().ApplyAsync(projection), database.Store().ApplyAsync(projection));

        Assert.Single(result, item => item.Disposition == AiTechnicalProjectionApplyDisposition.Applied);
        Assert.Single(result, item => item.Disposition == AiTechnicalProjectionApplyDisposition.Replayed);
        await using var read = database.Factory.CreateDbContext();
        Assert.Single(await read.Set<Party>().ToListAsync());
        Assert.Single(await read.Set<AiResourceBinding>().ToListAsync());
    }

    [Fact]
    public async Task Failed_flush_rolls_back_cursor_reservation_and_projection_together() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var failure = new FlushFailure();
        var projection = database.Projection(1, Entry(Guid.NewGuid()));

        Assert.Same(failure.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => database.Store(failure).ApplyAsync(projection)));

        await using (var read = database.Factory.CreateDbContext()) {
            Assert.Empty(await read.Set<Party>().ToListAsync());
            Assert.Empty(await read.Set<AiResourceBinding>().ToListAsync());
            Assert.Empty(await read.Set<AiTechnicalProjectionCursor>().ToListAsync());
        }

        Assert.Equal(AiTechnicalProjectionApplyDisposition.Applied, (await database.Store().ApplyAsync(projection)).Disposition);
    }

    [Fact]
    public async Task Missing_agent_retains_history_but_is_not_live_or_a_legacy_repair_candidate() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var agent = Entry(Guid.NewGuid());
        await database.Store().ApplyAsync(database.Projection(1, agent));
        await database.Store().ApplyAsync(database.Projection(2));

        var resource = Assert.Single(await database.Store().ReadAsync([agent.PreferredPartyId!.Value])).Value;
        Assert.False(resource.Directory.HasTechnicalProfile);
        Assert.Equal(agent.TechnicalAgentId, resource.Directory.TechnicalAgentId);
        Assert.Equal(AiTechnicalProjectionAvailability.Missing, resource.Directory.Projection!.Availability);
        Assert.Empty(resource.Staffing.Instructions);
        Assert.Equal(0, await database.Store().CountBoundAsync());
        Assert.Empty((await database.Store().ReadCatalogRepairFactsAsync()).AiPartyIds);
        await using var read = database.Factory.CreateDbContext();
        var binding = await read.Set<AiResourceBinding>().SingleAsync();
        Assert.Equal(agent.Instructions, binding.ProjectedInstructions);
        Assert.Equal(agent.TechnicalAgentId, binding.TechnicalAgentId);
    }

    [Fact]
    public async Task Different_source_scopes_never_order_each_others_revisions() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var first = Entry(Guid.NewGuid());
        var second = Entry(Guid.NewGuid());
        await database.Store().ApplyAsync(database.Projection(100, first));
        var other = database.Projection(1, second) with {
            Source = new(database.Profile.Profile.Id, WorkspaceScopeDescriptor.Organization("another-source"))
        };

        Assert.Equal(AiTechnicalProjectionApplyDisposition.Applied, (await database.Store().ApplyAsync(other)).Disposition);
        Assert.Equal(2, await database.Store().CountBoundAsync());
        await using var read = database.Factory.CreateDbContext();
        Assert.Equal(2, await read.Set<AiTechnicalProjectionCursor>().CountAsync());
        Assert.All(await read.Set<AiResourceBinding>().ToListAsync(), binding => Assert.Equal(AiTechnicalProjectionAvailability.Present, binding.ProjectionAvailability));
    }

    [Fact]
    public async Task Wrong_pinned_profile_is_rejected_before_any_write() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var projection = database.Projection(1, Entry(Guid.NewGuid())) with {
            Source = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Organization("fixture"))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Store().ApplyAsync(projection));

        await using var read = database.Factory.CreateDbContext();
        Assert.Empty(await read.Set<AiTechnicalProjectionCursor>().ToListAsync());
    }

    [Fact]
    public async Task Clone_with_original_party_hint_cannot_rebind_the_original_human_resource() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var original = Entry(Guid.NewGuid());
        await database.Store().ApplyAsync(database.Projection(1, original));
        var clone = original with { TechnicalAgentId = Guid.NewGuid(), DisplayName = "Clone" };

        await database.Store().ApplyAsync(database.Projection(2, original, clone));
        await database.Store().ApplyAsync(database.Projection(3, clone, original));

        await using var read = database.Factory.CreateDbContext();
        var bindings = await read.Set<AiResourceBinding>().ToListAsync();
        Assert.Equal(original.TechnicalAgentId, Assert.Single(bindings, binding => binding.PartyId == original.PreferredPartyId).TechnicalAgentId);
        Assert.NotEqual(original.PreferredPartyId, Assert.Single(bindings, binding => binding.TechnicalAgentId == clone.TechnicalAgentId).PartyId);
        Assert.Equal(2, await database.Store().CountBoundAsync());
    }

    [Fact]
    public async Task Legacy_bound_projection_remains_readable_without_fabricating_provenance() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var partyId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext()) {
            seed.Add(new Party { Id = partyId, PartyType = PartyType.AiAgent, DisplayName = "Legacy", CreatedAtUtc = Now, UpdatedAtUtc = Now });
            seed.Add(new AiResourceBinding {
                PartyId = partyId, TechnicalAgentId = Guid.NewGuid(), BindingStatus = AiResourceBindingStatus.Bound,
                ProjectedInstructions = "Historical instructions", ProjectionUpdatedAtUtc = Now, CreatedAtUtc = Now, UpdatedAtUtc = Now
            });
            await seed.SaveChangesAsync();
        }

        var resource = Assert.Single(await database.Store().ReadAsync([partyId])).Value;

        Assert.True(resource.Directory.HasTechnicalProfile);
        Assert.Null(resource.Directory.Projection);
        Assert.Equal("Historical instructions", resource.Staffing.Instructions);
        Assert.Single((await database.Store().ReadCatalogRepairFactsAsync()).Bindings);
    }

    [Fact]
    public async Task Legacy_pending_backfill_adopts_the_actual_catalog_identity_without_replacing_the_party() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var partyId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext()) {
            seed.Add(new Party { Id = partyId, PartyType = PartyType.AiAgent, DisplayName = "Legacy local name", CreatedAtUtc = Now, UpdatedAtUtc = Now });
            seed.Add(new AiAgentProfile { Id = legacyId, PartyId = partyId, Notes = "Legacy governance" });
            seed.Add(new AiResourceBinding { PartyId = partyId, TechnicalAgentId = legacyId,
                BindingStatus = AiResourceBindingStatus.PendingBackfill, CreatedAtUtc = Now, UpdatedAtUtc = Now });
            await seed.SaveChangesAsync();
        }

        var agent = Entry(partyId);
        await database.Store().ApplyAsync(database.Projection(1, agent));

        await using var read = database.Factory.CreateDbContext();
        Assert.Equal(partyId, (await read.Set<Party>().SingleAsync()).Id);
        Assert.Equal("Legacy local name", (await read.Set<Party>().SingleAsync()).DisplayName);
        Assert.Equal(agent.TechnicalAgentId, (await read.Set<AiResourceBinding>().SingleAsync()).TechnicalAgentId);
        Assert.Equal(legacyId, (await read.Set<AiAgentProfile>().SingleAsync()).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_catalog_and_restore_keep_the_original_party_and_superseded_binding(bool winnerHasLowerId) {
        await using var database = await ProjectionDatabase.CreateAsync();
        var winnerPartyId = Guid.NewGuid();
        var duplicatePartyId = Guid.NewGuid();
        var agent = Entry(winnerPartyId);
        var lowerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var higherId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var winnerId = winnerHasLowerId ? lowerId : higherId;
        var duplicateId = winnerHasLowerId ? higherId : lowerId;
        await using (var seed = database.Factory.CreateDbContext()) {
            seed.AddRange(
                new Party { Id = winnerPartyId, PartyType = PartyType.AiAgent, DisplayName = "Original human resource", Notes = "Original governance" },
                new Party { Id = duplicatePartyId, PartyType = PartyType.AiAgent, DisplayName = "Duplicate human resource", Notes = "Duplicate governance" });
            seed.AddRange(
                new AiResourceBinding {
                    Id = winnerId, PartyId = winnerPartyId, TechnicalAgentId = agent.TechnicalAgentId,
                    BindingStatus = AiResourceBindingStatus.Bound, UpdatedAtUtc = Now
                },
                new AiResourceBinding {
                    Id = duplicateId, PartyId = duplicatePartyId, TechnicalAgentId = agent.TechnicalAgentId,
                    BindingStatus = AiResourceBindingStatus.Error, ProjectedInstructions = "Retained duplicate instructions", UpdatedAtUtc = Now.AddDays(-1)
                });
            await seed.SaveChangesAsync();
        }

        await database.Store().ApplyAsync(database.Projection(1, agent));
        string retainedDuplicate;
        await using (var firstRead = database.Factory.CreateDbContext()) {
            var duplicate = await firstRead.Set<AiResourceBinding>().SingleAsync(binding => binding.Id == duplicateId);
            Assert.Equal(AiTechnicalProjectionAvailability.Superseded, duplicate.ProjectionAvailability);
            retainedDuplicate = JsonSerializer.Serialize(duplicate);
        }

        await database.Store().ApplyAsync(database.Projection(2));
        await database.Store().ApplyAsync(database.Projection(3));
        await using (var missingRead = database.Factory.CreateDbContext()) {
            Assert.Equal(retainedDuplicate, JsonSerializer.Serialize(await missingRead.Set<AiResourceBinding>().SingleAsync(binding => binding.Id == duplicateId)));
            Assert.Equal(AiTechnicalProjectionAvailability.Missing,
                (await missingRead.Set<AiResourceBinding>().SingleAsync(binding => binding.Id == winnerId)).ProjectionAvailability);
        }

        await database.Store().ApplyAsync(database.Projection(4, agent));
        await using var restartedRead = database.Factory.CreateDbContext();
        var bindings = await restartedRead.Set<AiResourceBinding>().ToArrayAsync();
        var winner = Assert.Single(bindings, binding => binding.BindingStatus == AiResourceBindingStatus.Bound);
        Assert.Equal(winnerId, winner.Id);
        Assert.Equal(winnerPartyId, winner.PartyId);
        Assert.Equal(agent.TechnicalAgentId, winner.TechnicalAgentId);
        var superseded = Assert.Single(bindings, binding => binding.Id == duplicateId);
        Assert.Equal(AiTechnicalProjectionAvailability.Superseded, superseded.ProjectionAvailability);
        Assert.Equal("Retained duplicate instructions", superseded.ProjectedInstructions);
        Assert.Equal("Original governance", (await restartedRead.Set<Party>().SingleAsync(party => party.Id == winnerPartyId)).Notes);
        Assert.Equal("Duplicate governance", (await restartedRead.Set<Party>().SingleAsync(party => party.Id == duplicatePartyId)).Notes);
    }

    [Fact]
    public async Task Superseded_history_cannot_be_promoted_when_the_original_binding_is_missing() {
        await using var database = await ProjectionDatabase.CreateAsync();
        var partyId = Guid.NewGuid();
        var agent = Entry(partyId);
        await using (var seed = database.Factory.CreateDbContext()) {
            seed.Add(new Party { Id = partyId, PartyType = PartyType.AiAgent, DisplayName = "Retained duplicate" });
            seed.Add(new AiResourceBinding {
                PartyId = partyId, TechnicalAgentId = agent.TechnicalAgentId, BindingStatus = AiResourceBindingStatus.Error,
                ProjectionAvailability = AiTechnicalProjectionAvailability.Superseded, ProjectedInstructions = "Historical instructions"
            });
            await seed.SaveChangesAsync();
        }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Store().ApplyAsync(database.Projection(1, agent)));

        Assert.Contains("only superseded CRM bindings", failure.Message);
        await using var restarted = database.Factory.CreateDbContext();
        Assert.Empty(await restarted.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
        Assert.Single(await restarted.Set<Party>().ToArrayAsync());
        var retained = await restarted.Set<AiResourceBinding>().SingleAsync();
        Assert.Equal(AiTechnicalProjectionAvailability.Superseded, retained.ProjectionAvailability);
        Assert.Equal(AiResourceBindingStatus.Error, retained.BindingStatus);
        Assert.Equal("Historical instructions", retained.ProjectedInstructions);
    }

    private static AiTechnicalProjectionEntry Entry(Guid partyId)
        => new(Guid.NewGuid(), partyId, "Technical Agent", "Technical summary", AgentLifecycleStatus.Active,
            AiExecutionMode.Remote, "Provider", "model", "Role", "Technical instructions", "template", ["runtime-tag"],
            [new("Capability", "Scope", "Tool", "Limitation", "Notes")]);

    private sealed class ProjectionDatabase : ICanonicalRuntimeDatabase, IAsyncDisposable {
        private readonly PostgresTestDatabaseLease lease = PostgresTestDatabaseLease.Create("crm-technical-projection");
        private ProjectionDatabase() {
            Profile = new(new() { DisplayName = "CRM projection", ProviderKind = DatabaseProviderKind.PostgreSql,
                SourceKind = DatabaseProfileSourceKind.PostgresConnection, PostgreSql = new() { DatabaseName = lease.DatabaseName } },
                DatabaseProfileResolutionSource.ExplicitOverride, lease.ConnectionString);
            Factory = new(new DbContextOptionsBuilder<CrmHrDbContext>().UseNpgsql(lease.ConnectionString).Options);
        }

        public ResolvedDatabaseProfile Profile { get; }
        public long Generation => 0;
        public Factory Factory { get; }
        public AiTechnicalAgentProjectionStore Store(IInterceptor? interceptor = null)
            => new(interceptor is null ? Factory : new Factory(new DbContextOptionsBuilder<CrmHrDbContext>(Factory.Options).AddInterceptors(interceptor).Options), this, new Clock());
        public AiTechnicalCatalogProjection Projection(long revision, params AiTechnicalProjectionEntry[] agents)
            => new(new(Profile.Profile.Id, WorkspaceScopeDescriptor.Organization("fixture")), new(revision), agents.ToImmutableArray());

        public static async Task<ProjectionDatabase> CreateAsync() {
            var database = new ProjectionDatabase();
            try {
                await using var context = database.Factory.CreateDbContext();
                await context.Database.EnsureCreatedAsync();
                return database;
            } catch {
                await database.DisposeAsync();
                throw;
            }
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }

    private sealed class Factory(DbContextOptions<CrmHrDbContext> options) : IDbContextFactory<CrmHrDbContext> {
        public DbContextOptions<CrmHrDbContext> Options { get; } = options;
        public CrmHrDbContext CreateDbContext() => new(Options);
    }

    private sealed class Clock : IClock {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FlushFailure : SaveChangesInterceptor {
        public InvalidOperationException Failure { get; } = new("Reject projection flush.");
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw Failure;
    }

    private sealed class SaveGate : SaveChangesInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class CursorAttempt : DbCommandInterceptor {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("INSERT INTO \"CrmHr_AiTechnicalProjectionCursors\"", StringComparison.Ordinal)) {
                Entered.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
