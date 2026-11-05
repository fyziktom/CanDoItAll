using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrRelationshipConcurrentEditingTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Stale_relationship_edit_or_delete_keeps_the_opposite_endpoint_writers_commit(bool delete, bool oppositeEndpoint) {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var firstParty = await CreatePartyAsync(directory, "Relationship subject");
        var secondParty = await CreatePartyAsync(directory, "Relationship peer");
        var loser = first.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var winner = second.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        Assert.True((await loser.SaveRelationshipsAsync(firstParty,
            [new() { RelatedPartyId = secondParty, Notes = "Original relationship" }], "relationship-test", original: new([]))).IsSuccess);
        var original = await loser.ListRelationshipsAsync(firstParty);
        var winningParty = oppositeEndpoint ? secondParty : firstParty;
        var winningOriginal = await winner.ListRelationshipsAsync(winningParty);
        var winningRows = winningOriginal.Select(Editor).ToList();
        winningRows[0].Notes = "Peer committed relationship";
        Assert.True((await winner.SaveRelationshipsAsync(winningParty, winningRows, "relationship-test", original: PartyRelationshipObservation.FromRows(winningOriginal))).IsSuccess);
        var stale = delete ? [] : original.Select(Editor).ToList();
        if (!delete) {
            stale[0].Notes = "Stale local relationship";
        }

        var result = await loser.SaveRelationshipsAsync(firstParty, stale, "relationship-test", original: PartyRelationshipObservation.FromRows(original));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        var stored = Assert.Single(await winner.ListRelationshipsAsync(firstParty));
        Assert.Equal("Peer committed relationship", stored.Notes);
        Assert.Equal(original[0].Id, stored.Id);
    }

    [Fact]
    public async Task A_foreign_insertion_is_not_removed_by_a_stale_empty_replacement() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Empty relationship subject");
        var peer = await CreatePartyAsync(directory, "Inserted relationship peer");
        var loser = first.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var winner = second.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var original = await loser.ListRelationshipsAsync(subject);
        Assert.Empty(original);
        Assert.True((await winner.SaveRelationshipsAsync(peer,
            [new() { RelatedPartyId = subject, Notes = "Foreign inserted relationship" }], "relationship-test", original: new([]))).IsSuccess);

        var result = await loser.SaveRelationshipsAsync(subject, [], "relationship-test", original: PartyRelationshipObservation.FromRows(original));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        Assert.Equal("Foreign inserted relationship", Assert.Single(await winner.ListRelationshipsAsync(subject)).Notes);
    }

    [Fact]
    public async Task Full_replacement_requires_an_explicit_original_even_for_an_empty_set() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Unobserved relationship subject");
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();

        var result = await management.SaveRelationshipsAsync(subject, [], "relationship-test");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.party.relationship-original-required");
    }

    [Fact]
    public async Task Native_row_versions_preserve_retained_identities_and_explicit_deletion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Versioned relationship subject");
        var peer = await CreatePartyAsync(directory, "Versioned relationship peer");
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var retainedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var created = await management.SaveRelationshipsAsync(subject, [
            new() { Id = retainedId, RelatedPartyId = peer, RelationshipKind = PartyRelationshipKind.PartnerOf },
            new() { Id = removedId, RelatedPartyId = peer, RelationshipKind = PartyRelationshipKind.Supports }
        ], "relationship-test", original: new([]));
        Assert.True(created.IsSuccess);
        Assert.All(created.Value!.Accepted, row => Assert.NotEqual(0u, row.Version));
        var retained = Editor(created.Value.Accepted.Single(row => row.Id == retainedId));
        retained.Notes = "  Canonical retained note  ";

        var result = await management.SaveRelationshipsAsync(subject, [retained], "relationship-test",
            original: PartyRelationshipObservation.FromRows(created.Value.Accepted));

        Assert.True(result.IsSuccess);
        var accepted = Assert.Single(result.Value!.Accepted);
        Assert.Equal(retainedId, accepted.Id);
        Assert.Equal("Canonical retained note", accepted.Notes);
        Assert.NotEqual(created.Value.Accepted.Single(row => row.Id == retainedId).Version, accepted.Version);
        Assert.DoesNotContain(await management.ListRelationshipsAsync(peer), row => row.Id == removedId);
    }

    [Fact]
    public async Task Restoring_the_same_values_does_not_restore_an_older_original_row_version() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "ABA relationship subject");
        var peer = await CreatePartyAsync(directory, "ABA relationship peer");
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var created = await management.SaveRelationshipsAsync(subject, [new() { RelatedPartyId = peer, Notes = "Before" }],
            "relationship-test", original: new([]));
        Assert.True(created.IsSuccess);
        var original = created.Value!.Accepted;
        var edited = Editor(Assert.Single(original));
        edited.Notes = "Between";
        var changed = await management.SaveRelationshipsAsync(subject, [edited], "relationship-test", original: PartyRelationshipObservation.FromRows(original));
        Assert.True(changed.IsSuccess);
        edited.Notes = "Before";
        var restored = await management.SaveRelationshipsAsync(subject, [edited], "relationship-test", original: PartyRelationshipObservation.FromRows(changed.Value!.Accepted));
        Assert.True(restored.IsSuccess);

        var stale = await management.SaveRelationshipsAsync(subject, original.Select(Editor).ToList(), "relationship-test", original: PartyRelationshipObservation.FromRows(original));

        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        Assert.Equal("Before", Assert.Single(await management.ListRelationshipsAsync(subject)).Notes);
    }

    [Fact]
    public async Task Forged_original_endpoints_cannot_omit_the_actual_row_from_exclusion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Witness relationship subject");
        var peer = await CreatePartyAsync(directory, "Witness relationship peer");
        var management = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var created = await management.SaveRelationshipsAsync(subject, [new() { RelatedPartyId = peer, Notes = "Keep witness" }], "relationship-test", original: new([]));
        Assert.True(created.IsSuccess);
        var observed = PartyRelationshipObservation.FromRows(created.Value!.Accepted);
        observed = new(observed.Rows.Select(row => row with { RelatedPartyId = Guid.NewGuid() }).ToArray());

        var result = await management.SaveRelationshipsAsync(subject, [], "relationship-test", original: observed);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipConflict);
        Assert.Equal("Keep witness", Assert.Single(await management.ListRelationshipsAsync(subject)).Notes);
    }

    [Fact]
    public async Task Existing_migration_schema_supports_the_native_system_version_without_a_user_column() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var version = context.Model.FindEntityType(typeof(PartyRelationship))!.FindProperty(nameof(PartyRelationship.Version))!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
        var userColumns = await context.Database.SqlQuery<string>($"""
            SELECT column_name AS "Value" FROM information_schema.columns
            WHERE table_schema='public' AND table_name='CrmHr_PartyRelationships'
            """).ToListAsync();
        Assert.DoesNotContain("Version", userColumns);
        Assert.DoesNotContain("xmin", userColumns);
        await using var root = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.False(root.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Accepted_relationship_state_is_captured_before_a_later_commit() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Accepted relationship subject");
        var peer = await CreatePartyAsync(directory, "Accepted relationship peer");
        var management = services.GetRequiredService<PartyDirectoryManagementService>();
        var created = await management.SaveRelationshipsAsync(subject, [new() { RelatedPartyId = peer, Notes = "Original" }], "relationship-test", original: new([]));
        Assert.True(created.IsSuccess);
        var options = services.GetRequiredService<DbContextOptions<CrmHrDbContext>>();
        var gate = new CommitGate();
        var owner = new PartyRelationshipService(new InterceptedFactory(options, gate), services.GetRequiredService<IClock>(), services.GetRequiredService<CrmHrChanges>());
        var submitted = Editor(Assert.Single(created.Value!.Accepted));
        submitted.Notes = "My accepted relationship";
        var pending = owner.ReplaceAsync(subject, [submitted], PartyRelationshipObservation.FromRows(created.Value.Accepted), "relationship-test");
        try {
            await gate.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var later = new CrmHrDbContext(options);
            var row = await later.Set<PartyRelationship>().SingleAsync(row => row.Id == submitted.Id);
            row.Notes = "Later committed relationship";
            await later.SaveChangesAsync();
        } finally {
            gate.Release.TrySetResult();
        }
        var result = await pending;

        Assert.True(result.IsSuccess);
        Assert.Equal("My accepted relationship", Assert.Single(result.Value!.Accepted).Notes);
        Assert.Equal("Later committed relationship", Assert.Single(await management.ListRelationshipsAsync(subject)).Notes);
    }

    [Fact]
    public async Task An_automatic_read_budget_never_returns_a_partial_relationship_observation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var subject = await CreatePartyAsync(directory, "Bounded relationship subject");
        var first = await CreatePartyAsync(directory, "First bounded peer");
        var second = await CreatePartyAsync(directory, "Second bounded peer");
        var owner = scope.ServiceProvider.GetRequiredService<IPartyRelationshipService>();
        var saved = await owner.ReplaceAsync(subject, [new() { RelatedPartyId = first }, new() { RelatedPartyId = second }], new([]), "relationship-test");
        Assert.True(saved.IsSuccess);

        var exceeded = await Assert.ThrowsAsync<RelationshipReadLimitExceededException>(() => owner.ListAsync(subject, maximumRows: 1));
        var exact = await owner.ListAsync(subject, maximumRows: 2);
        var complete = await owner.ListAsync(subject);

        Assert.Equal(1, exceeded.MaximumRows);
        Assert.Equal(2, exact.Count);
        Assert.Equal(PartyRelationshipObservation.FromRows(saved.Value!.Accepted).Rows.OrderBy(row => row.Id),
            PartyRelationshipObservation.FromRows(complete).Rows.OrderBy(row => row.Id));
        Assert.Equal(complete, exact);
    }

    private sealed class InterceptedFactory(DbContextOptions<CrmHrDbContext> options, CommitGate gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class CommitGate : DbTransactionInterceptor {
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            Committed.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private static PartyRelationshipEditorModel Editor(PartyRelationshipListItemModel row) => new() {
        Id = row.Id, RelatedPartyId = row.RelatedPartyId, RelatedPartyDisplayName = row.RelatedPartyDisplayName,
        RelatedPartyType = row.RelatedPartyType, RelationshipKind = row.RelationshipKind, IsOutgoing = row.IsOutgoing,
        IsPrimary = row.IsPrimary, StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc, Notes = row.Notes
    };

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService directory, string name) {
        var result = await directory.SavePartyAsync(new() { DisplayName = name });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
