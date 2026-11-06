using System.Data.Common;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrOpportunityReceiptTests {
    [Fact]
    public async Task Acceptance_contains_canonical_values_and_saved_link_ids_without_mutating_the_submission() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Title = "  Canonical opportunity receipt  ";
        submitted.CurrencyCode = " usd ";
        submitted.Notes = "  Canonical notes  ";
        submitted.LastChangedBy = " receipt-test ";
        submitted.Parties.Add(new() { PartyId = original.OwnerPartyId, Role = OpportunityPartyRole.Partner });
        CrmOpportunitySaveReceipt? committed = null;

        var result = await owner.SaveOpportunityWithReceiptAsync(submitted, onCommitted: receipt => committed = receipt);

        Assert.True(result.IsSuccess);
        Assert.NotNull(committed);
        Assert.Equal(original.Id, result.Value!.OpportunityId);
        Assert.Equal("Canonical opportunity receipt", committed.Accepted.Title);
        Assert.Equal("USD", committed.Accepted.CurrencyCode);
        Assert.Equal("Canonical notes", committed.Accepted.Notes);
        Assert.Equal("receipt-test", committed.Accepted.LastChangedBy);
        Assert.NotEqual(Guid.Empty, Assert.Single(committed.Accepted.Parties).Id);
        Assert.NotNull(Assert.Single(committed.Accepted.Parties).Id);
        Assert.NotSame(submitted, committed.Accepted);
        Assert.Equal("  Canonical notes  ", submitted.Notes);
        Assert.Null(Assert.Single(submitted.Parties).Id);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityForEditingAsync(original.Id));
        Assert.Equal(current.UpdatedAtUtc, committed.Accepted.ExpectedUpdatedAtUtc);
        Assert.Equal(Assert.Single(current.Parties).Id, Assert.Single(committed.Accepted.Parties).Id);
    }

    [Fact]
    public async Task The_commit_callback_observes_a_real_write_before_a_continuation_failure() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Title = "Known committed opportunity";
        CrmOpportunitySaveReceipt? committed = null;

        await Assert.ThrowsAsync<InjectedFailure>(() => owner.SaveOpportunityWithReceiptAsync(submitted, onCommitted: receipt => {
            committed = receipt;
            throw new InjectedFailure();
        }));

        Assert.NotNull(committed);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityForEditingAsync(original.Id));
        Assert.Equal("Known committed opportunity", current.Title);
        Assert.Equal(current.UpdatedAtUtc, committed.Accepted.ExpectedUpdatedAtUtc);
        Assert.Equal(current.Title, committed.Accepted.Title);
        Assert.True((await owner.SaveOpportunityWithReceiptAsync(submitted)).IsFailure);
    }

    [Fact]
    public async Task An_automatic_read_never_returns_partial_links_or_loads_immutable_stage_history() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Parties = [
            new() { PartyId = original.OwnerPartyId, Role = OpportunityPartyRole.Partner },
            new() { PartyId = original.OwnerPartyId, Role = OpportunityPartyRole.BillingContact }
        ];
        var saved = await owner.SaveOpportunityWithReceiptAsync(submitted);
        Assert.True(saved.IsSuccess);

        var exceeded = await Assert.ThrowsAsync<OpportunityReadLimitExceededException>(() => owner.GetOpportunityForEditingAsync(original.Id, maximumPartyLinks: 1));
        var complete = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityForEditingAsync(original.Id));
        var exact = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityForEditingAsync(original.Id, maximumPartyLinks: 2));

        Assert.Equal(1, exceeded.MaximumRows);
        Assert.Equal(2, complete.Parties.Count);
        Assert.Equal(complete.Parties, exact.Parties);
        Assert.Equal(saved.Value!.Accepted.Parties.Select(row => row.Id).Order(), complete.Parties.Select(row => (Guid?)row.Id).Order());
        Assert.Empty(complete.StageHistory);
        Assert.NotEmpty((await scope.ServiceProvider.GetRequiredService<CrmService>().GetOpportunityAsync(original.Id))!.StageHistory);
    }

    [Fact]
    public async Task A_later_writer_cannot_be_mistaken_for_the_accepted_opportunity_state() {
        var gate = new CommitGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new InterceptedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(first.ServiceProvider);
        var owner = first.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var peer = second.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Title = CommitGate.AcceptedTitle;
        var saving = owner.SaveOpportunityWithReceiptAsync(submitted);
        await gate.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            var after = Assert.IsType<CrmOpportunityDetailModel>(await peer.GetOpportunityForEditingAsync(original.Id));
            var later = CrmHrOpportunityConcurrentEditingTests.Edit(after);
            later.Title = "Later committed opportunity";
            Assert.True((await peer.SaveOpportunityWithReceiptAsync(later)).IsSuccess);
        } finally {
            gate.Release.TrySetResult();
        }

        var result = await saving;
        var current = Assert.IsType<CrmOpportunityDetailModel>(await peer.GetOpportunityForEditingAsync(original.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(CommitGate.AcceptedTitle, result.Value!.Accepted.Title);
        Assert.Equal("Later committed opportunity", current.Title);
        Assert.True(current.UpdatedAtUtc > result.Value.Accepted.ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task A_revoked_ui_write_admission_cannot_commit_or_publish_a_receipt() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<ICrmOpportunityEditorService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Title = "A write whose UI admission was revoked";
        CrmOpportunitySaveReceipt? committed = null;
        using var admission = scope.ServiceProvider.GetRequiredService<CrmHrChanges>()
            .EnterUiWrite(_ => ValueTask.FromException(new UnauthorizedAccessException("The test UI grant was revoked.")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.SaveOpportunityWithReceiptAsync(submitted, onCommitted: receipt => committed = receipt));

        Assert.Null(committed);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityForEditingAsync(original.Id));
        Assert.Equal(original.Title, current.Title);
        Assert.Equal(original.UpdatedAtUtc, current.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_after_SQL_before_physical_commit_rolls_back_the_editor_or_stage_intent(bool stageIntent) {
        var revoked = false;
        var gate = new PostSaveRevocation { Revoke = () => revoked = true };
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new InterceptedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<CrmService>();
        var submitted = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        submitted.Notes = "This SQL must not become an authorized commit";
        var published = false;
        using var admission = scope.ServiceProvider.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => revoked ?
            ValueTask.FromException(new UnauthorizedAccessException("The current grant was revoked before physical commit.")) : ValueTask.CompletedTask);
        gate.Armed = true;

        if (stageIntent) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.AdvanceOpportunityStageAsync(
                new(original.Id, original.AccountPartyId, original.Stage, original.UpdatedAtUtc), "commit-admission-test", onCommitted: _ => published = true));
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.SaveOpportunityWithReceiptAsync(submitted, onCommitted: _ => published = true));
        }

        Assert.True(gate.ObservedTransaction);
        Assert.False(published);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(original.UpdatedAtUtc, current.UpdatedAtUtc);
        Assert.Equal(original.Notes, current.Notes);
        Assert.Equal(original.Stage, current.Stage);
        Assert.Equal(original.StageHistory.Count, current.StageHistory.Count);
    }

    private sealed class InjectedFailure : Exception;

    private sealed class InterceptedFactory(DbContextOptions<CrmHrDbContext> options, IInterceptor gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class PostSaveRevocation : SaveChangesInterceptor {
        public bool Armed { get; set; }
        public bool ObservedTransaction { get; private set; }
        public required Action Revoke { get; init; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context?.ChangeTracker.Entries<Opportunity>().Any() == true) {
                Armed = false;
                ObservedTransaction = eventData.Context.Database.CurrentTransaction is not null;
                Revoke();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitGate : DbTransactionInterceptor {
        public const string AcceptedTitle = "My accepted opportunity";
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (eventData.Context?.ChangeTracker.Entries<Opportunity>().Any(entry => entry.Entity.Title == AcceptedTitle) == true) {
                Committed.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }
}
