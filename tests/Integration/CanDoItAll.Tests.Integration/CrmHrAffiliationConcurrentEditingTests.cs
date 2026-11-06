using System.Data.Common;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrAffiliationConcurrentEditingTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_locally_identified_new_row_retains_its_identity_and_expected_absence_never_replaces_an_existing_row(bool batch) {
        await using var application = await CreateApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (service, original) = await CreateAffiliationAsync(scope.ServiceProvider);
        var fresh = Editor(original);
        fresh.Id = Guid.NewGuid();
        fresh.IsNew = true;
        fresh.ExpectedUpdatedAtUtc = null;
        fresh.AffiliationKind = PartyOrganizationAffiliationKind.Employee;
        fresh.JobTitle = "New identified affiliation";

        var success = batch
            ? (await service.ReplaceAsync(original.PersonPartyId, [Editor(original), fresh], "affiliation-test")).IsSuccess
            : (await service.UpsertAsync(fresh, "affiliation-test")).IsSuccess;

        Assert.True(success);
        var stored = await service.ListAsync(original.PersonPartyId);
        var created = Assert.Single(stored, row => row.Id == fresh.Id);
        Assert.Equal("New identified affiliation", created.JobTitle);
        fresh.JobTitle = "Replay must not replace the row";
        var rejected = batch
            ? (await service.ReplaceAsync(original.PersonPartyId, [Editor(stored.Single(row => row.Id == original.Id)), fresh], "affiliation-test")).IsFailure
            : (await service.UpsertAsync(fresh, "affiliation-test")).IsFailure;
        Assert.True(rejected);
        Assert.Equal("New identified affiliation", (await service.ListAsync(original.PersonPartyId)).Single(row => row.Id == fresh.Id).JobTitle);
    }

    [Fact]
    public async Task Existing_affiliation_upsert_requires_its_observed_version() {
        await using var application = await CreateApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (service, original) = await CreateAffiliationAsync(scope.ServiceProvider);
        var replacement = Editor(original);
        replacement.ExpectedUpdatedAtUtc = null;
        replacement.JobTitle = "Unobserved replacement";

        var result = await service.UpsertAsync(replacement, "affiliation-test");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.affiliation.concurrency-version-required");
        Assert.Equal(original.JobTitle, Assert.Single(await service.ListAsync(original.PersonPartyId)).JobTitle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fixed_clock_replacements_advance_the_observation_and_reject_the_stale_writer(bool batch) {
        await using var application = await CreateApplicationAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var (loser, original) = await CreateAffiliationAsync(first.ServiceProvider);
        var winner = second.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        var edited = Editor(original);
        edited.JobTitle = "Committed title";
        var accepted = await winner.UpsertAsync(edited, "affiliation-test");
        Assert.True(accepted.IsSuccess);
        var stale = Editor(original);
        stale.JobTitle = "Stale title";

        var result = batch
            ? (await loser.ReplaceAsync(original.PersonPartyId, [stale], "affiliation-test")).IsFailure
            : (await loser.UpsertAsync(stale, "affiliation-test")).IsFailure;

        Assert.True(result);
        Assert.True(accepted.Value!.UpdatedAtUtc > original.UpdatedAtUtc);
        var stored = Assert.Single(await winner.ListAsync(original.PersonPartyId));
        Assert.Equal("Committed title", stored.JobTitle);
        Assert.Equal(original.Id, stored.Id);
    }

    [Fact]
    public async Task Replacement_cannot_remove_an_affiliation_inserted_by_another_writer() {
        await using var application = await CreateApplicationAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var (loser, original) = await CreateAffiliationAsync(first.ServiceProvider);
        var directory = second.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var otherOrganization = await directory.SavePartyAsync(new PartyEditorModel { DisplayName = "Other affiliation organization", PartyType = PartyType.Organization });
        Assert.True(otherOrganization.IsSuccess);
        var winner = second.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        Assert.True((await winner.UpsertAsync(new() {
            PersonPartyId = original.PersonPartyId, OrganizationPartyId = otherOrganization.Value, JobTitle = "Foreign inserted row"
        }, "affiliation-test")).IsSuccess);

        var result = await loser.ReplaceAsync(original.PersonPartyId, [Editor(original)], "affiliation-test");

        Assert.True(result.IsFailure);
        var stored = await winner.ListAsync(original.PersonPartyId);
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, row => row.JobTitle == "Foreign inserted row");
    }

    [Fact]
    public async Task Accepted_affiliation_values_cannot_be_replaced_by_a_read_after_a_later_commit() {
        await using var application = await CreateApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (_, original) = await CreateAffiliationAsync(scope.ServiceProvider);
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<CrmHrDbContext>>();
        var gate = new CommitGate();
        var writer = new PartyOrganizationAffiliationService(new InterceptedFactory(options, gate), new FixedClock(),
            scope.ServiceProvider.GetRequiredService<CrmHrChanges>());
        var submitted = Editor(original);
        submitted.JobTitle = "My accepted title";

        var pending = writer.UpsertAsync(submitted, "affiliation-test");
        try {
            await gate.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var later = new CrmHrDbContext(options);
            var entity = await later.Set<PartyOrganizationAffiliation>().SingleAsync(row => row.Id == original.Id);
            entity.JobTitle = "Later writer title";
            entity.UpdatedAtUtc = entity.UpdatedAtUtc.AddTicks(10);
            await later.SaveChangesAsync();
        } finally {
            gate.Release.TrySetResult();
        }

        var result = await pending;

        Assert.True(result.IsSuccess);
        Assert.Equal("My accepted title", result.Value!.JobTitle);
        var service = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        Assert.Equal("Later writer title", Assert.Single(await service.ListAsync(original.PersonPartyId)).JobTitle);
    }

    private static Task<TestApplication> CreateApplicationAsync() => TestApplication.CreateAsync(new() {
        ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton<IClock>(new FixedClock()))
    });

    private static async Task<(IPartyOrganizationAffiliationService Service, PartyOrganizationAffiliationListItemModel Original)> CreateAffiliationAsync(IServiceProvider services) {
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var person = await directory.SavePartyAsync(new PartyEditorModel { DisplayName = "Affiliation person", PartyType = PartyType.Person });
        var organization = await directory.SavePartyAsync(new PartyEditorModel { DisplayName = "Affiliation organization", PartyType = PartyType.Organization });
        Assert.True(person.IsSuccess);
        Assert.True(organization.IsSuccess);
        var service = services.GetRequiredService<IPartyOrganizationAffiliationService>();
        var created = await service.UpsertAsync(new() { PersonPartyId = person.Value, OrganizationPartyId = organization.Value, JobTitle = "Original title" }, "affiliation-test");
        Assert.True(created.IsSuccess);
        return (service, created.Value!);
    }

    private static PartyOrganizationAffiliationEditorModel Editor(PartyOrganizationAffiliationListItemModel row) => new() {
        Id = row.Id, PersonPartyId = row.PersonPartyId, OrganizationPartyId = row.OrganizationPartyId,
        AffiliationKind = row.AffiliationKind, IsPrimary = row.IsPrimary, JobTitle = row.JobTitle, EmployeeCode = row.EmployeeCode,
        OrganizationUnitPartyId = row.OrganizationUnitPartyId, ManagerPartyId = row.ManagerPartyId,
        ValidFrom = row.ValidFrom, ValidTo = row.ValidTo, Notes = row.Notes, ExpectedUpdatedAtUtc = row.UpdatedAtUtc
    };

    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
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
}
