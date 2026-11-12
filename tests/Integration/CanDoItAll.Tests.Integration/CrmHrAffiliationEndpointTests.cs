using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrAffiliationEndpointTests {
    [Theory]
    [InlineData(Endpoint.Person, false)]
    [InlineData(Endpoint.Organization, false)]
    [InlineData(Endpoint.Unit, false)]
    [InlineData(Endpoint.Manager, false)]
    [InlineData(Endpoint.Person, true)]
    [InlineData(Endpoint.Organization, true)]
    [InlineData(Endpoint.Unit, true)]
    [InlineData(Endpoint.Manager, true)]
    public async Task Party_replacement_cannot_invalidate_saved_affiliation_endpoints(Endpoint endpoint, bool import) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var affiliations = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        var person = await CreatePartyAsync(directory, PartyType.Person);
        var organization = await CreatePartyAsync(directory, PartyType.Organization);
        var unit = await CreatePartyAsync(directory, PartyType.OrganizationUnit);
        var manager = await CreatePartyAsync(directory, PartyType.Person);
        var targetId = endpoint switch {
            Endpoint.Person => person,
            Endpoint.Organization => organization,
            Endpoint.Unit => unit,
            Endpoint.Manager => manager,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
        var original = (await directory.GetPartyAsync(targetId))!;
        Assert.True((await affiliations.UpsertAsync(new() {
            PersonPartyId = person, OrganizationPartyId = organization,
            OrganizationUnitPartyId = unit, ManagerPartyId = manager
        }, "endpoint-test")).IsSuccess);
        var edited = original.Snapshot();
        edited.PartyType = PartyType.AiAgent;
        edited.DisplayName = "Rejected type change";

        var result = import
            ? (await directory.ImportPartiesAtomicallyAsync([edited], "endpoint-test")).Errors
            : (await directory.SavePartyAsync(edited)).Errors;

        Assert.Contains(result, error => error.Code == "crmhr.party.affiliation-type-conflict");
        var stored = (await directory.GetPartyAsync(targetId))!;
        Assert.Equal(original.PartyType, stored.PartyType);
        Assert.Equal(original.UpdatedAtUtc, stored.UpdatedAtUtc);
        Assert.Equal(original.DisplayName, stored.DisplayName);
        Assert.Single(await affiliations.ListAsync(person));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Concurrent_type_change_and_affiliation_creation_cannot_both_commit(bool affiliationFirst, bool replace, bool import) {
        var pause = new SavePause(affiliationFirst);
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new PausedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), pause));
            }
        });
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var person = await CreatePartyAsync(directory, PartyType.Person);
        var organization = await CreatePartyAsync(directory, PartyType.Organization);
        var edited = (await directory.GetPartyAsync(organization))!;
        edited.PartyType = PartyType.OrganizationUnit;
        var affiliations = second.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        pause.Armed = true;
        var firstWrite = affiliationFirst ? SaveAffiliationAsync() : SaveTypeAsync();
        Task<bool>? secondWrite = null;
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            secondWrite = affiliationFirst ? SaveTypeAsync() : SaveAffiliationAsync();
            await Task.WhenAny(secondWrite, Task.Delay(TimeSpan.FromMilliseconds(150)));
        } finally {
            pause.Release.TrySetResult();
        }
        var results = await Task.WhenAll(firstWrite, secondWrite!).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(results.All(value => value));
        var stored = (await directory.GetPartyAsync(organization))!;
        var saved = await affiliations.ListAsync(person);
        if (saved.Count > 0) {
            Assert.Equal(PartyType.Organization, stored.PartyType);
            Assert.Equal(organization, Assert.Single(saved).OrganizationPartyId);
        } else {
            Assert.Equal(PartyType.OrganizationUnit, stored.PartyType);
        }

        async Task<bool> SaveAffiliationAsync() {
            PartyOrganizationAffiliationEditorModel affiliation = new() { PersonPartyId = person, OrganizationPartyId = organization };
            return replace
                ? (await affiliations.ReplaceAsync(person, [affiliation], "endpoint-test")).IsSuccess
                : (await affiliations.UpsertAsync(affiliation, "endpoint-test")).IsSuccess;
        }
        async Task<bool> SaveTypeAsync() => import
            ? (await directory.ImportPartiesAtomicallyAsync([edited], "endpoint-test")).IsSuccess
            : (await directory.SavePartyAsync(edited)).IsSuccess;
    }

    [Fact]
    public async Task Unreferenced_type_changes_and_referenced_name_edits_remain_available() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var person = await CreatePartyAsync(directory, PartyType.Person);
        var organization = await CreatePartyAsync(directory, PartyType.OrganizationUnit);
        var edited = (await directory.GetPartyAsync(organization))!;
        edited.PartyType = PartyType.Organization;
        Assert.True((await directory.SavePartyAsync(edited)).IsSuccess);
        var affiliations = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        Assert.True((await affiliations.UpsertAsync(new() {
            PersonPartyId = person, OrganizationPartyId = organization
        }, "endpoint-test")).IsSuccess);
        edited = (await directory.GetPartyAsync(organization))!;
        edited.DisplayName = "Renamed valid organization";

        Assert.True((await directory.SavePartyAsync(edited)).IsSuccess);
        Assert.Equal(edited.DisplayName, Assert.Single(await affiliations.ListAsync(person)).OrganizationDisplayName);
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService directory, PartyType type) {
        var created = await directory.SavePartyAsync(new() { PartyType = type, DisplayName = $"Affiliation endpoint {type}" });
        Assert.True(created.IsSuccess);
        return created.Value;
    }

    public enum Endpoint { Person, Organization, Unit, Manager }

    private sealed class PausedFactory(DbContextOptions<CrmHrDbContext> options, SavePause pause) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(pause).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class SavePause(bool affiliationFirst) : SaveChangesInterceptor {
        private int entered;
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            var context = eventData.Context!;
            if (Armed && (affiliationFirst
                ? context.ChangeTracker.Entries<PartyOrganizationAffiliation>().Any(entry => entry.State == EntityState.Added)
                : context.ChangeTracker.Entries<Party>().Any(entry => entry.State == EntityState.Modified && entry.Entity.PartyType == PartyType.OrganizationUnit)) &&
                Interlocked.CompareExchange(ref entered, 1, 0) == 0) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
