using System.Data.Common;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrOpportunityConcurrentEditingTests {
    private const string ConflictCode = "crmhr.crm.opportunity-concurrency-conflict";

    [Fact]
    public async Task A_fixed_clock_update_advances_the_native_opportunity_observation() {
        await using var application = await CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var original = await SeedAsync(scope.ServiceProvider);
        var submitted = Edit(original);
        submitted.Title = "First accepted opportunity";

        Assert.True((await crm.SaveOpportunityAsync(submitted)).IsSuccess);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));

        Assert.True(current.UpdatedAtUtc > original.UpdatedAtUtc);
        Assert.Equal(0, current.UpdatedAtUtc.Ticks % 10);
        Assert.Equal("First accepted opportunity", current.Title);
    }

    [Fact]
    public async Task A_fixed_clock_stale_editor_cannot_overwrite_an_accepted_opportunity() {
        await using var application = await CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var original = await SeedAsync(first.ServiceProvider);
        var winner = first.ServiceProvider.GetRequiredService<CrmService>();
        var loser = second.ServiceProvider.GetRequiredService<CrmService>();
        var accepted = Edit(original);
        accepted.Notes = "Accepted first editor notes";
        var stale = Edit(original);
        stale.Notes = "Stale second editor notes";
        Assert.True((await winner.SaveOpportunityAsync(accepted)).IsSuccess);

        var result = await loser.SaveOpportunityAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == ConflictCode);
        Assert.Equal("Accepted first editor notes", (await winner.GetOpportunityAsync(original.Id))!.Notes);
    }

    [Fact]
    public async Task The_owner_freezes_validated_input_before_its_first_await() {
        var gate = new CreationGate();
        await using var application = await CreateAsync(gate);
        await using var scope = application.Services.CreateAsyncScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var original = await SeedAsync(scope.ServiceProvider);
        var submitted = Edit(original);
        submitted.Title = "Submitted opportunity title";
        submitted.Notes = "Submitted opportunity notes";
        gate.Armed = true;

        var saving = crm.SaveOpportunityAsync(submitted);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        submitted.Title = "Caller title typed after submission";
        submitted.Notes = "Caller notes typed after submission";
        gate.Release.TrySetResult();
        Assert.True((await saving).IsSuccess);
        var stored = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));

        Assert.Equal("Submitted opportunity title", stored.Title);
        Assert.Equal("Submitted opportunity notes", stored.Notes);
        Assert.Equal("Caller title typed after submission", submitted.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_party_merge_invalidates_an_opportunity_draft_whose_reference_it_rewrites(bool ownerReference) {
        await using var application = await CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await SeedAsync(services);
        var independentOwnerId = original.OwnerPartyId;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var retained = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Retained opportunity reference" });
        var merged = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Merged opportunity reference" });
        Assert.True(retained.IsSuccess);
        Assert.True(merged.IsSuccess);
        var linked = Edit(original);
        if (ownerReference) {
            linked.OwnerPartyId = merged.Value;
        } else {
            linked.Parties.Add(new() { PartyId = merged.Value, Role = OpportunityPartyRole.Partner });
        }
        Assert.True((await crm.SaveOpportunityAsync(linked)).IsSuccess);
        original = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        var stale = Edit(original);
        stale.Title = "Stale opportunity after party merge";
        if (ownerReference) {
            stale.OwnerPartyId = independentOwnerId;
        } else {
            stale.Parties.Clear();
        }

        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var preview = await manager.PreviewMergeAsync(retained.Value, merged.Value);
        Assert.True(preview.IsSuccess);
        var merge = await manager.MergePartyAsync(retained.Value, merged.Value, "Reconcile duplicate reference", "opportunity-test",
            original: preview.Value!.Original);
        Assert.True(merge.IsSuccess);
        var result = await crm.SaveOpportunityAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == ConflictCode);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        if (ownerReference) {
            Assert.Equal(retained.Value, current.OwnerPartyId);
        } else {
            Assert.Equal(retained.Value, Assert.Single(current.Parties).PartyId);
        }
    }

    [Fact]
    public async Task A_fixed_clock_conversion_cannot_be_unlinked_by_a_preconversion_editor() {
        await using var application = await CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var original = await SeedAsync(scope.ServiceProvider);
        var won = Edit(original);
        won.Stage = OpportunityStage.Won;
        won.ProbabilityPercent = 100;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        original = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        var stale = Edit(original);
        stale.Notes = "Stale edit before project conversion";

        var conversion = await crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = original.Id, ExpectedUpdatedAtUtc = original.UpdatedAtUtc, ProjectName = "Opportunity conversion witness"
        });
        Assert.True(conversion.IsSuccess);
        var result = await crm.SaveOpportunityAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == ConflictCode);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        Assert.Equal(conversion.Value!.ProjectId, current.LinkedProjectId);
        Assert.True(current.UpdatedAtUtc > original.UpdatedAtUtc);
    }

    [Fact]
    public async Task A_current_read_cannot_mix_an_older_header_with_new_party_links() {
        var gate = new SnapshotReadGate();
        await using var application = await CreateAsync(readGate: gate);
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var reader = first.ServiceProvider.GetRequiredService<CrmService>();
        var writer = second.ServiceProvider.GetRequiredService<CrmService>();
        var original = await SeedAsync(first.ServiceProvider);
        gate.Armed = true;
        var reading = reader.GetOpportunityAsync(original.Id);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {
            var peer = Edit(original);
            peer.Title = "Peer opportunity header";
            peer.Parties.Add(new() { PartyId = original.OwnerPartyId, Role = OpportunityPartyRole.Partner });
            Assert.True((await writer.SaveOpportunityAsync(peer)).IsSuccess);
        } finally {
            gate.Release.TrySetResult();
        }

        var observed = Assert.IsType<CrmOpportunityDetailModel>(await reading);

        Assert.Equal(original.Title, observed.Title);
        Assert.Empty(observed.Parties);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await writer.GetOpportunityAsync(original.Id));
        Assert.Equal("Peer opportunity header", current.Title);
        Assert.Single(current.Parties);
    }

    private static Task<TestApplication> CreateAsync(CreationGate? gate = null, SnapshotReadGate? readGate = null) => TestApplication.CreateAsync(new() {
        ConfigureServices = services => {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock());
            if (gate is not null || readGate is not null) {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate, readGate));
            }
        }
    });

    internal static async Task<CrmOpportunityDetailModel> SeedAsync(IServiceProvider services) {
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var account = await directory.SavePartyAsync(new() { PartyType = PartyType.Organization, DisplayName = "Opportunity concurrency account" });
        var owner = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Opportunity concurrency owner" });
        Assert.True(account.IsSuccess);
        Assert.True(owner.IsSuccess);
        var crm = services.GetRequiredService<CrmService>();
        var saved = await crm.SaveOpportunityAsync(new() {
            AccountPartyId = account.Value, OwnerPartyId = owner.Value, Title = "Original opportunity", CurrencyCode = "USD", Amount = 125m
        });
        Assert.True(saved.IsSuccess);
        return Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(saved.Value));
    }

    internal static CrmOpportunityEditorModel Edit(CrmOpportunityDetailModel source) => new() {
        Id = source.Id, ExpectedUpdatedAtUtc = source.UpdatedAtUtc, AccountPartyId = source.AccountPartyId,
        OwnerPartyId = source.OwnerPartyId, Title = source.Title, Stage = source.Stage, CurrencyCode = source.CurrencyCode,
        Amount = source.Amount, ProbabilityPercent = source.ProbabilityPercent, Notes = source.Notes,
        LinkedProjectId = source.LinkedProjectId,
        LinkedProjectAdmission = source.LinkedProjectAdmission,
        Parties = source.Parties.Select(row => new CrmOpportunityPartyLinkEditorModel { Id = row.Id, PartyId = row.PartyId, Role = row.Role }).ToList()
    };

    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    }

    private sealed class CreationGate {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, CreationGate? gate, SnapshotReadGate? readGate) : IDbContextFactory<CrmHrDbContext> {
        private DbContextOptions<CrmHrDbContext> ContextOptions => readGate is null ? options :
            new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(readGate).Options;
        public CrmHrDbContext CreateDbContext() => new(ContextOptions);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate?.Armed == true) {
                gate.Armed = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return new(ContextOptions);
        }
    }

    private sealed class SnapshotReadGate : DbCommandInterceptor {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (!Armed) {
                return result;
            }
            var table = eventData.Context?.Model.FindEntityType(typeof(OpportunityPartyLink))?.GetTableName()
                ?? throw new InvalidOperationException("The read gate requires the mapped Opportunity link table.");
            if (command.CommandText.Contains($"\"{table}\"", StringComparison.Ordinal)) {
                Armed = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
