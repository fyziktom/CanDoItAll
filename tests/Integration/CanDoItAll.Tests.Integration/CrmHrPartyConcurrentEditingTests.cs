using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrPartyConcurrentEditingTests {
    [Fact]
    public async Task Cleared_contact_rows_are_removed_atomically_and_do_not_conflict_with_a_new_primary_contact() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await service.SavePartyAsync(new PartyEditorModel {
            DisplayName = "Cleared contact owner",
            ContactPoints = [new() { ContactType = PartyContactType.Email, Value = "before@example.test", IsPrimary = true }]
        });
        Assert.True(created.IsSuccess);
        var original = (await service.GetPartyAsync(created.Value))!;
        var edited = original.Snapshot();
        edited.ContactPoints[0].Value = "  ";
        edited.ContactPoints.Add(new() { ContactType = PartyContactType.Email, Value = " after@example.test ", IsPrimary = true });

        var result = await service.SavePartyWithReceiptAsync(edited);

        Assert.True(result.IsSuccess);
        var accepted = Assert.Single(result.Value!.Accepted.ContactPoints);
        Assert.Equal("after@example.test", accepted.Value);
        Assert.NotEqual(original.ContactPoints[0].Id, accepted.Id);
        var stored = (await service.GetPartyAsync(created.Value))!;
        Assert.Equal(accepted.Id, Assert.Single(stored.ContactPoints).Id);
        Assert.Equal(result.Value.Accepted.UpdatedAtUtc, stored.UpdatedAtUtc);
        original.ContactPoints[0].Value = "stale@example.test";
        Assert.True((await service.SavePartyAsync(original)).IsFailure);
        Assert.Equal("after@example.test", Assert.Single((await service.GetPartyAsync(created.Value))!.ContactPoints).Value);
    }

    [Fact]
    public async Task A_writer_that_loses_at_flush_cannot_commit_any_of_its_children() {
        var gate = new PartySaveGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var loser = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var winner = second.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await winner.SavePartyAsync(new PartyEditorModel { DisplayName = "Race owner" });
        Assert.True(created.IsSuccess);
        var losingDraft = Assert.IsType<PartyEditorModel>(await loser.GetPartyAsync(created.Value));
        var winningDraft = losingDraft.Snapshot();
        losingDraft.DisplayName = PartySaveGate.PausedName;
        losingDraft.ContactPoints.Add(new() { ContactType = PartyContactType.Email, Value = "loser@example.test" });
        winningDraft.DisplayName = "Flush winner";
        winningDraft.Roles.Add(new() { RoleKind = PartyRoleKind.Customer });
        var pending = loser.SavePartyAsync(losingDraft);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True((await winner.SavePartyAsync(winningDraft)).IsSuccess);
        } finally {
            gate.Release.TrySetResult();
        }
        var result = await pending;

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyConflict);
        var stored = Assert.IsType<PartyEditorModel>(await winner.GetPartyAsync(created.Value));
        Assert.Equal(winningDraft.DisplayName, stored.DisplayName);
        Assert.Single(stored.Roles);
        Assert.Empty(stored.ContactPoints);
    }

    [Fact]
    public async Task Each_commit_advances_the_original_even_with_an_unchanged_clock() {
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => services.AddSingleton<IClock>(new FrozenClock())
        });
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await service.SavePartyAsync(new PartyEditorModel {
            DisplayName = "Frozen clock", IsSensitive = true,
            Roles = [new() { RoleKind = PartyRoleKind.Customer, ValidFromUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(1)).AddTicks(7) }],
            ConfidentialNotes = [new() { NoteText = "Precise accepted note" }]
        });
        Assert.True(created.IsSuccess);
        var original = Assert.IsType<PartyEditorModel>(await service.GetPartyAsync(created.Value));
        var edited = original.Snapshot();
        edited.DisplayName = "First save";
        var result = await service.SavePartyWithReceiptAsync(edited);
        Assert.True(result.IsSuccess);
        var stored = Assert.IsType<PartyEditorModel>(await service.GetPartyAsync(created.Value));
        Assert.Equal(stored.UpdatedAtUtc, result.Value!.Accepted.UpdatedAtUtc);
        Assert.Equal(stored.Roles[0].ValidFromUtc, result.Value.Accepted.Roles[0].ValidFromUtc);
        Assert.Equal(stored.ConfidentialNotes[0].CreatedAtUtc, result.Value.Accepted.ConfidentialNotes[0].CreatedAtUtc);
        Assert.Equal(stored.ConfidentialNotes[0].UpdatedAtUtc, result.Value.Accepted.ConfidentialNotes[0].UpdatedAtUtc);

        Assert.True((await service.SavePartyAsync(original)).IsFailure);
        Assert.True((await service.GetPartyAsync(created.Value))?.UpdatedAtUtc > original.UpdatedAtUtc);
    }

    [Fact]
    public async Task Stale_replacement_keeps_the_committed_party_and_children() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var firstWriter = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var secondWriter = second.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await firstWriter.SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Person,
            DisplayName = "Concurrent party",
            IsSensitive = true
        });
        Assert.True(created.IsSuccess);
        var stale = Assert.IsType<PartyEditorModel>(await firstWriter.GetPartyAsync(created.Value));
        var winner = Assert.IsType<PartyEditorModel>(await secondWriter.GetPartyAsync(created.Value));
        winner.DisplayName = "Committed name";
        winner.Roles.Add(new() { RoleKind = PartyRoleKind.Customer, Title = "Committed role" });
        winner.ContactPoints.Add(new() { ContactType = PartyContactType.Email, Value = "committed@example.test" });
        winner.Addresses.Add(new() { Line1 = "Committed address" });
        winner.ConfidentialNotes.Add(new() { NoteText = "Committed confidential note" });
        Assert.True((await secondWriter.SavePartyAsync(winner)).IsSuccess);

        stale.DisplayName = "Stale replacement";
        var result = await firstWriter.SavePartyAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.party.concurrency-conflict");
        var stored = Assert.IsType<PartyEditorModel>(await secondWriter.GetPartyAsync(created.Value));
        Assert.Equal(winner.DisplayName, stored.DisplayName);
        Assert.Equal("Committed role", Assert.Single(stored.Roles).Title);
        Assert.Equal("committed@example.test", Assert.Single(stored.ContactPoints).Value);
        Assert.Equal("Committed address", Assert.Single(stored.Addresses).Line1);
        Assert.Equal("Committed confidential note", Assert.Single(stored.ConfidentialNotes).NoteText);
    }

    [Fact]
    public async Task Existing_replacement_requires_the_original_observation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await service.SavePartyAsync(new PartyEditorModel { DisplayName = "Original" });
        Assert.True(created.IsSuccess);

        var result = await service.SavePartyAsync(new PartyEditorModel { Id = created.Value, DisplayName = "Unobserved" });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.party.original-required");
        Assert.Equal("Original", (await service.GetPartyAsync(created.Value))?.DisplayName);
    }

    [Fact]
    public async Task Current_replacement_preserves_child_ids_and_supports_row_deletion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var created = await service.SavePartyAsync(new PartyEditorModel {
            DisplayName = "Child owner",
            Roles = [new() { RoleKind = PartyRoleKind.Customer }],
            ContactPoints = [new() { ContactType = PartyContactType.Email, Value = "first@example.test" }],
            Addresses = [new() { Line1 = "Retained address" }, new() { Line1 = "Removed address" }]
        });
        Assert.True(created.IsSuccess);
        var original = Assert.IsType<PartyEditorModel>(await service.GetPartyAsync(created.Value));
        var edited = original.Snapshot();
        edited.ContactPoints[0].Value = "edited@example.test";
        edited.Addresses.RemoveAll(row => row.Line1 == "Removed address");

        Assert.True((await service.SavePartyAsync(edited)).IsSuccess);

        var stored = Assert.IsType<PartyEditorModel>(await service.GetPartyAsync(created.Value));
        Assert.Equal(original.Roles[0].Id, Assert.Single(stored.Roles).Id);
        Assert.Equal(original.ContactPoints[0].Id, Assert.Single(stored.ContactPoints).Id);
        Assert.Equal("edited@example.test", stored.ContactPoints[0].Value);
        Assert.Equal(original.Addresses.Single(row => row.Line1 == "Retained address").Id, Assert.Single(stored.Addresses).Id);
        Assert.NotEqual(original.UpdatedAtUtc, stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task Account_customer_role_invalidates_an_older_directory_replacement() {
        await using var application = await TestApplication.CreateAsync();
        await using var directoryScope = application.Services.CreateAsyncScope();
        await using var accountScope = application.Services.CreateAsyncScope();
        var directory = directoryScope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crm = accountScope.ServiceProvider.GetRequiredService<CrmService>();
        var created = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Account" });
        Assert.True(created.IsSuccess);
        var stale = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(created.Value));
        var account = Assert.IsType<CrmAccountWorkspaceModel>(await crm.GetAccountWorkspaceAsync(created.Value));
        account.Profile.RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer;
        Assert.True((await crm.SaveAccountProfileAsync(account.Profile)).IsSuccess);

        var result = await directory.SavePartyAsync(stale);

        Assert.True(result.IsFailure);
        var stored = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(created.Value));
        Assert.Equal(PartyLifecycleStatus.Active, stored.LifecycleStatus);
        Assert.Contains(stored.Roles, role => role.RoleKind == PartyRoleKind.Customer);
    }

    private sealed class FrozenClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, PartySaveGate gate)
        : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);

        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class PartySaveGate : SaveChangesInterceptor {
        public const string PausedName = "Paused writer";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<Party>().Any(entry => entry.Entity.DisplayName == PausedName)) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
