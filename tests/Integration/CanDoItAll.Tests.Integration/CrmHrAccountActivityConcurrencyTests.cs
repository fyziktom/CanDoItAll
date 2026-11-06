using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrAccountActivityConcurrencyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revoked_connection_authority_rolls_back_before_or_after_SQL_without_an_accepted_receipt(bool afterSql) {
        var gate = new AppendGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();
        var original = (await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectionsOriginal;
        gate.ObserveConnections = true;
        var accepted = false;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterSql || gate.ConnectionsSaved) {
                throw new UnauthorizedAccessException("The connection grant retired.");
            }
            return ValueTask.CompletedTask;
        });

        var failure = await Record.ExceptionAsync(() => crm.SaveConnectedRecordsAsync(source.AccountPartyId,
            [new() { RelatedPartyId = source.OwnerPartyId }], "integration-tests", original: original,
            onCommitted: _ => accepted = true));

        Assert.IsType<UnauthorizedAccessException>(failure);
        Assert.False(accepted);
        if (afterSql) {
            Assert.True(gate.ConnectionsSaved);
            Assert.True(gate.HadConnectionTransaction);
        }
        Assert.Empty((await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectedRecords);
    }

    [Fact]
    public async Task Actual_project_retirement_cannot_be_undone_by_a_stale_connection_replacement() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();
        var project = (await services.GetRequiredService<CanDoItAll.Modules.Projects.ProjectsService>()
            .CreateWithAdmissionAsync(new() { Name = "Connection lifecycle target" })).Value!;
        CrmAccountConnectionsSaveReceipt? accepted = null;
        Assert.True((await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [new() {
            Id = Guid.NewGuid(), RelatedPartyId = source.OwnerPartyId, ProjectIds = [project.ProjectId],
            ProjectAdmissions = new() { [project.ProjectId] = project }
        }], "integration-tests", original: (await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectionsOriginal,
            onCommitted: receipt => accepted = receipt)).IsSuccess);
        Assert.NotNull(accepted);
        await new ProjectLifecycleTestDriver(services).DeleteAsync(project.ProjectId);

        var stale = await crm.SaveConnectedRecordsAsync(source.AccountPartyId, accepted.Accepted, "stale", original: accepted.Original);

        Assert.True(stale.IsFailure);
        var current = Assert.Single((await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectedRecords);
        Assert.Empty(current.Projects);
        Assert.Equal(accepted.Accepted[0].Id, current.Id);
    }

    [Fact]
    public async Task An_append_cannot_attach_an_opportunity_from_another_account_or_a_missing_participant() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var account = await directory.SavePartyAsync(new() { PartyType = PartyType.Organization, DisplayName = "Append target account" });
        var other = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();

        var foreign = await crm.AddInteractionAsync(account.Value, new() { Subject = "Wrong account", RelatedOpportunityId = other.Id }, "integration-tests");
        var missing = await crm.AddInteractionAsync(account.Value, new() { Subject = "Missing participant", ParticipantPartyIds = [Guid.NewGuid()] }, "integration-tests");

        Assert.True(foreign.IsFailure);
        Assert.True(missing.IsFailure);
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<InteractionRecord>().AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_saved_account_reference_cannot_be_invalidated_by_a_later_party_type_change(bool journal) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var account = await directory.SavePartyAsync(new() { PartyType = PartyType.Organization, DisplayName = "Account reference owner" });
        Assert.True(account.IsSuccess);
        var crm = services.GetRequiredService<CrmService>();
        if (journal) {
            Assert.True((await crm.AddInteractionAsync(account.Value, new() { Subject = "Typed account journal" }, "integration-tests")).IsSuccess);
        } else {
            var person = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Account reference contact" });
            Assert.True(person.IsSuccess);
            Assert.True((await crm.SaveConnectedRecordsAsync(account.Value, [new() { RelatedPartyId = person.Value }], "integration-tests",
                original: (await crm.GetAccountWorkspaceAsync(account.Value))!.ConnectionsOriginal)).IsSuccess);
        }
        var edit = (await directory.GetPartyAsync(account.Value))!;
        edit.PartyType = PartyType.Person;

        var changed = await directory.SavePartyAsync(edit);

        Assert.True(changed.IsFailure);
        Assert.Equal(PartyType.Organization, (await directory.GetPartyAsync(account.Value))!.PartyType);
        Assert.NotNull(await crm.GetAccountWorkspaceAsync(account.Value));
    }

    [Fact]
    public async Task A_stale_empty_connection_set_cannot_remove_a_peer_addition() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();
        var original = (await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectionsOriginal;
        var created = await crm.SaveConnectedRecordsAsync(source.AccountPartyId,
            [new() { RelatedPartyId = source.OwnerPartyId, Notes = "Peer connection" }], "peer", original: original);
        Assert.True(created.IsSuccess);

        var stale = await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [], "stale", original: original);

        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == CrmHrEditingErrorCodes.ConnectionConflict);
        var retained = Assert.Single((await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectedRecords);
        Assert.Equal("Peer connection", retained.Notes);
    }

    [Fact]
    public async Task A_connection_receipt_retains_stable_row_and_project_link_identity_and_cannot_be_replaced_by_a_stale_edit() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();
        var project = (await services.GetRequiredService<CanDoItAll.Modules.Projects.ProjectsService>()
            .CreateWithAdmissionAsync(new() { Name = "Observed account connection" })).Value!;
        var id = Guid.NewGuid();
        var original = (await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectionsOriginal;
        CrmAccountConnectionsSaveReceipt? accepted = null;
        Assert.True((await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [new() {
            Id = id, RelatedPartyId = source.OwnerPartyId, Notes = "  Original notes  ",
            ProjectIds = [project.ProjectId], ProjectAdmissions = new() { [project.ProjectId] = project }
        }], "first", original: original, onCommitted: receipt => accepted = receipt)).IsSuccess);
        Assert.NotNull(accepted);
        var saved = Assert.Single(accepted.Accepted);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Original notes", saved.Notes);
        var projectLink = Assert.Single(accepted.Original.Projects);
        var peer = saved.Snapshot();
        peer.Notes = "Peer revised notes";
        CrmAccountConnectionsSaveReceipt? current = null;
        Assert.True((await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [peer], "peer",
            original: accepted.Original, onCommitted: receipt => current = receipt)).IsSuccess);
        Assert.NotNull(current);
        Assert.Equal(projectLink, Assert.Single(current.Original.Projects));
        saved.Notes = "Stale notes must stay local";

        var stale = await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [saved], "stale", original: accepted.Original);

        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == CrmHrEditingErrorCodes.ConnectionConflict);
        Assert.Equal("Peer revised notes", Assert.Single((await crm.GetAccountWorkspaceAsync(source.AccountPartyId))!.ConnectedRecords).Notes);
        Assert.Equal("Stale notes must stay local", saved.Notes);
    }

    [Fact]
    public async Task Interaction_receipt_identifies_exact_canonical_values_before_a_failed_continuation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        CrmInteractionAppendReceipt? accepted = null;

        var failure = await Record.ExceptionAsync(() => services.GetRequiredService<CrmService>().AddInteractionAsync(
            source.AccountPartyId, new() {
                Subject = "  Accepted interaction  ", Summary = "  Canonical summary  ",
                ParticipantPartyIds = [source.OwnerPartyId, source.OwnerPartyId, Guid.Empty, source.AccountPartyId]
            }, "integration-tests", onCommitted: receipt => {
                accepted = receipt;
                throw new ContinuationFailure();
            }));

        Assert.IsType<ContinuationFailure>(failure);
        Assert.NotNull(accepted);
        Assert.Equal(source.AccountPartyId, accepted.AccountPartyId);
        Assert.Equal("Accepted interaction", accepted.Accepted.Subject);
        Assert.Equal("Canonical summary", accepted.Accepted.Summary);
        Assert.Equal([source.OwnerPartyId], accepted.Accepted.ParticipantPartyIds);
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.Equal(accepted.InteractionId, (await read.Set<InteractionRecord>().SingleAsync()).Id);
    }

    [Fact]
    public async Task Connection_replacement_requires_an_explicit_complete_original_even_for_an_empty_set() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);

        var result = await crm.SaveConnectedRecordsAsync(source.AccountPartyId, [], "integration-tests");

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.ConnectionOriginalRequired);
    }

    [Fact]
    public async Task Interaction_append_freezes_all_input_before_its_first_await() {
        var gate = new AppendGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var crm = services.GetRequiredService<CrmService>();
        var model = new CrmInteractionEditorModel {
            Subject = "  Submitted journal subject  ", Summary = "Submitted summary", ParticipantPartyIds = [source.OwnerPartyId]
        };
        gate.HoldCreation = true;
        var pending = crm.AddInteractionAsync(source.AccountPartyId, model, "integration-tests");
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        model.Subject = "Later local journal subject";
        model.Summary = "Later local summary";
        model.ParticipantPartyIds.Clear();
        gate.Release.TrySetResult();

        var result = await pending;

        Assert.True(result.IsSuccess);
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var saved = await read.Set<InteractionRecord>().SingleAsync(row => row.Id == result.Value);
        Assert.Equal("Submitted journal subject", saved.Subject);
        Assert.Equal("Submitted summary", saved.Summary);
        Assert.True(await read.Set<InteractionPartyLink>().AnyAsync(row => row.InteractionId == saved.Id && row.PartyId == source.OwnerPartyId));
        Assert.Equal("Later local journal subject", model.Subject);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revoked_interaction_authority_cannot_commit_the_append(bool afterSql) {
        var gate = new AppendGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        gate.ObserveAppend = true;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterSql || gate.Appended) {
                throw new UnauthorizedAccessException("The interaction grant retired.");
            }
            return ValueTask.CompletedTask;
        });

        var failure = await Record.ExceptionAsync(() => services.GetRequiredService<CrmService>().AddInteractionAsync(
            source.AccountPartyId, new() { Subject = "Revoked journal append" }, "integration-tests"));

        Assert.IsType<UnauthorizedAccessException>(failure);
        if (afterSql) {
            Assert.True(gate.Appended);
            Assert.True(gate.HadTransaction);
        }
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<InteractionRecord>().AnyAsync(row => row.Subject == "Revoked journal append"));
    }

    private static Task<TestApplication> CreateAsync(AppendGate gate) => TestApplication.CreateAsync(new() {
        ConfigureServices = services => {
            services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
            services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
        }
    });

    private sealed class ContinuationFailure : Exception;

    private sealed class AppendGate : SaveChangesInterceptor {
        public bool HoldCreation { get; set; }
        public bool ObserveAppend { get; set; }
        public bool ObserveConnections { get; set; }
        public bool ConnectionsSaved { get; private set; }
        public bool HadConnectionTransaction { get; private set; }
        public bool Appended { get; private set; }
        public bool HadTransaction { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (ObserveConnections && eventData.Context?.ChangeTracker.Entries<CrmAccountConnection>().Any() == true) {
                ConnectionsSaved = true;
                HadConnectionTransaction = eventData.Context.Database.CurrentTransaction is not null;
            }
            if (ObserveAppend && eventData.Context?.ChangeTracker.Entries<InteractionRecord>().Any() == true) {
                Appended = true;
                HadTransaction = eventData.Context.Database.CurrentTransaction is not null;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, AppendGate gate) : IDbContextFactory<CrmHrDbContext> {
        private DbContextOptions<CrmHrDbContext> Options => new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options;
        public CrmHrDbContext CreateDbContext() => new(Options);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.HoldCreation) {
                gate.HoldCreation = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return new(Options);
        }
    }
}
