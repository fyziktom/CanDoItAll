using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrCommitAuthorityTests {
    public enum Producer { Party, PartyType, Account, AffiliationReplace, AffiliationUpsert, Relationships }

    [Theory]
    [InlineData(Producer.Party)]
    [InlineData(Producer.PartyType)]
    [InlineData(Producer.Account)]
    [InlineData(Producer.AffiliationReplace)]
    [InlineData(Producer.AffiliationUpsert)]
    [InlineData(Producer.Relationships)]
    public async Task A_revoked_grant_after_SQL_cannot_publish_or_commit_an_earlier_R1_owner(Producer producer) {
        var revoked = false;
        var gate = new RevocationGate(() => revoked = true);
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new GatedFactory(
                    provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var opportunity = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var crm = services.GetRequiredService<CrmService>();
        var affiliations = services.GetRequiredService<IPartyOrganizationAffiliationService>();
        var relationships = services.GetRequiredService<IPartyRelationshipService>();
        var person = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(opportunity.OwnerPartyId));
        var account = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(opportunity.AccountPartyId));
        var profile = Assert.IsType<CrmAccountProfileEditorModel>(await crm.GetAccountProfileAsync(account.Id!.Value));
        var originalProfile = profile.Snapshot();
        var originalPersonVersion = person.UpdatedAtUtc;
        person.Notes = "Must roll back the pending party write";
        if (producer == Producer.PartyType) {
            person.PartyType = PartyType.OrganizationUnit;
        }
        profile.CommercialNotes = "Must roll back the pending account write";
        var affiliation = new PartyOrganizationAffiliationEditorModel {
            Id = Guid.NewGuid(), IsNew = true, PersonPartyId = person.Id!.Value, OrganizationPartyId = account.Id.Value,
            AffiliationKind = PartyOrganizationAffiliationKind.Employee, ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), JobTitle = "Denied affiliation"
        };
        var published = false;
        Func<Task> write = producer switch {
            Producer.Party or Producer.PartyType => async () => { await directory.SavePartyWithReceiptAsync(person, onCommitted: _ => published = true); },
            Producer.Account => async () => { await crm.SaveAccountProfileWithReceiptAsync(profile, onCommitted: _ => published = true); },
            Producer.AffiliationReplace => async () => { await affiliations.ReplaceAsync(person.Id.Value, [affiliation], "commit-admission-test", onCommitted: _ => published = true); },
            Producer.AffiliationUpsert => async () => { await affiliations.UpsertAsync(affiliation, "commit-admission-test", onCommitted: _ => published = true); },
            Producer.Relationships => async () => { await relationships.ReplaceAsync(person.Id.Value,
                [new() { Id = Guid.NewGuid(), RelatedPartyId = account.Id.Value, RelationshipKind = PartyRelationshipKind.MemberOf, IsOutgoing = true }],
                new([]), "commit-admission-test", onCommitted: _ => published = true); },
            _ => throw new ArgumentOutOfRangeException(nameof(producer))
        };
        using var admission = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => revoked ?
            ValueTask.FromException(new UnauthorizedAccessException("The native current grant was revoked after SQL.")) : ValueTask.CompletedTask);
        gate.Armed = true;

        var failure = await Record.ExceptionAsync(write);

        Assert.True(gate.Triggered, "The native producer must reach SQL before this authority boundary is evaluated.");
        Assert.IsType<UnauthorizedAccessException>(failure);
        Assert.True(gate.ObservedTransaction);
        Assert.False(published);
        var unchanged = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(person.Id.Value));
        Assert.Equal(originalPersonVersion, unchanged.UpdatedAtUtc);
        Assert.Equal(PartyType.Person, unchanged.PartyType);
        Assert.Empty(unchanged.Notes);
        Assert.Equal(account.UpdatedAtUtc, (await directory.GetPartyAsync(account.Id.Value))!.UpdatedAtUtc);
        Assert.Equal(originalProfile.Original, (await crm.GetAccountProfileAsync(account.Id.Value))!.Original);
        Assert.Empty(await affiliations.ListAsync(person.Id.Value));
        Assert.Empty(await relationships.ListAsync(person.Id.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stewardship_cannot_commit_or_emit_receipts_or_hints_when_authority_is_revoked_after_SQL(bool merge) {
        var revoked = false;
        var gate = new RevocationGate(() => revoked = true);
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new GatedFactory(
                    provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var retained = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = "Authority survivor" });
        var source = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = "Authority duplicate" });
        Assert.True(retained.IsSuccess);
        Assert.True(source.IsSuccess);
        var preview = (await manager.PreviewMergeAsync(retained.Value, source.Value)).Value!;
        var csv = (await manager.PreviewImportAsync("DisplayName,PartyType,LifecycleStatus\nAuthority import,Person,Active")).Value!;
        var changes = services.GetRequiredService<CrmHrChanges>();
        using var subscription = changes.Subscribe(CanDoItAll.Editing.EditingOwner.CrmHrParty);
        using var admission = changes.EnterUiWrite(_ => revoked
            ? ValueTask.FromException(new UnauthorizedAccessException("Revoked after native SQL.")) : ValueTask.CompletedTask);
        var receipts = 0;
        gate.Armed = true;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
            if (merge) {
                await manager.MergePartyAsync(retained.Value, source.Value, "authority-test", "Must roll back", original: preview.Original,
                    onCommitted: _ => receipts++);
            } else {
                await manager.ApplyImportAsync(csv.Rows, "authority-test", onCommitted: _ => receipts++);
            }
        });

        Assert.True(gate.Triggered);
        Assert.True(gate.ObservedTransaction);
        Assert.Equal(0, receipts);
        Assert.Empty((await parties.GetPartyAsync(retained.Value))!.Notes);
        Assert.NotNull(await parties.GetPartyAsync(source.Value));
        await using var verification = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.False(await verification.Set<Party>().AnyAsync(row => row.DisplayName == "Authority import"));
        using var cancellation = new CancellationTokenSource();
        var read = subscription.ReadAsync(cancellation.Token).AsTask();
        Assert.False(read.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
    }

    private sealed class RevocationGate(Action revoke) : SaveChangesInterceptor {
        public bool Armed { get; set; }
        public bool Triggered { get; private set; }
        public bool ObservedTransaction { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Triggered = true;
                ObservedTransaction = eventData.Context?.Database.CurrentTransaction is not null;
                revoke();
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, IInterceptor gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
