using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrAccountConcurrentEditingTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_profile_writer_losing_at_flush_cannot_commit_its_profile_lifecycle_or_customer_role(bool profileExists) {
        var gate = new ProfileSaveGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var loser = first.ServiceProvider.GetRequiredService<CrmService>();
        var winner = second.ServiceProvider.GetRequiredService<CrmService>();
        var created = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Profile flush race" });
        Assert.True(created.IsSuccess);
        if (profileExists) {
            Assert.True((await winner.SaveAccountProfileAsync((await winner.GetAccountProfileAsync(created.Value))!)).IsSuccess);
        }
        var local = (await loser.GetAccountProfileAsync(created.Value))!;
        var remote = local.Snapshot();
        local.CommercialNotes = ProfileSaveGate.PausedNotes;
        local.RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer;
        remote.CommercialNotes = "Flush winner";
        remote.RelationshipStage = CrmAccountRelationshipStage.DormantCustomer;
        var pending = loser.SaveAccountProfileAsync(local);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True((await winner.SaveAccountProfileAsync(remote)).IsSuccess);
        } finally {
            gate.Release.TrySetResult();
        }

        var result = await pending;

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.AccountConflict);
        var stored = (await winner.GetAccountProfileAsync(created.Value))!;
        Assert.Equal("Flush winner", stored.CommercialNotes);
        Assert.Equal(CrmAccountRelationshipStage.DormantCustomer, stored.RelationshipStage);
        var party = (await directory.GetPartyAsync(created.Value))!;
        Assert.Equal(PartyLifecycleStatus.Inactive, party.LifecycleStatus);
        Assert.DoesNotContain(party.Roles, row => row.RoleKind == PartyRoleKind.Customer);
    }

    [Fact]
    public async Task Concurrent_first_profile_creation_preserves_the_winning_stage_notes_and_customer_role() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var firstWriter = first.ServiceProvider.GetRequiredService<CrmService>();
        var secondWriter = second.ServiceProvider.GetRequiredService<CrmService>();
        var created = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "First profile" });
        Assert.True(created.IsSuccess);
        var stale = Assert.IsType<CrmAccountWorkspaceModel>(await firstWriter.GetAccountWorkspaceAsync(created.Value)).Profile;
        var winner = Assert.IsType<CrmAccountWorkspaceModel>(await secondWriter.GetAccountWorkspaceAsync(created.Value)).Profile;
        Assert.Null(stale.Id);
        winner.RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer;
        winner.CommercialNotes = "Accepted commercial notes";
        Assert.True((await secondWriter.SaveAccountProfileAsync(winner)).IsSuccess);
        stale.CommercialNotes = "Unobserved replacement";

        var result = await firstWriter.SaveAccountProfileAsync(stale);

        Assert.True(result.IsFailure);
        var stored = Assert.IsType<CrmAccountWorkspaceModel>(await firstWriter.GetAccountWorkspaceAsync(created.Value));
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, stored.Profile.RelationshipStage);
        Assert.Equal(winner.CommercialNotes, stored.Profile.CommercialNotes);
        Assert.Contains(PartyRoleKind.Customer, stored.Roles);
    }

    [Fact]
    public async Task Profile_save_does_not_rewrite_a_party_changed_in_the_directory() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var directory = first.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crm = second.ServiceProvider.GetRequiredService<CrmService>();
        var created = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Directory change" });
        Assert.True(created.IsSuccess);
        var profile = Assert.IsType<CrmAccountWorkspaceModel>(await crm.GetAccountWorkspaceAsync(created.Value)).Profile;
        var party = Assert.IsType<PartyEditorModel>(await directory.GetPartyAsync(created.Value));
        party.LifecycleStatus = PartyLifecycleStatus.Archived;
        Assert.True((await directory.SavePartyAsync(party)).IsSuccess);

        var result = await crm.SaveAccountProfileAsync(profile);

        Assert.True(result.IsFailure);
        Assert.Equal(PartyLifecycleStatus.Archived, (await directory.GetPartyAsync(created.Value))?.LifecycleStatus);
        Assert.Null((await crm.GetAccountWorkspaceAsync(created.Value))?.Profile.Id);
    }

    [Fact]
    public async Task Profile_replacement_requires_an_original_party_observation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var created = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Unobserved account" });
        Assert.True(created.IsSuccess);

        var result = await crm.SaveAccountProfileAsync(new CrmAccountProfileEditorModel {
            AccountPartyId = created.Value,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer
        });

        Assert.True(result.IsFailure);
        Assert.Null((await crm.GetAccountWorkspaceAsync(created.Value))?.Profile.Id);
    }
    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, ProfileSaveGate gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class ProfileSaveGate : SaveChangesInterceptor {
        public const string PausedNotes = "Paused profile writer";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<CrmAccountProfile>().Any(entry => entry.Entity.CommercialNotes == PausedNotes)) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
