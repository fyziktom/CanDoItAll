using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class CrmAgentCommittedSaveTests {
    public enum FailureStage { CrmWrite, Search, Activity, CancelledSearch, RejectedBeforeSave }

    [Theory]
    [InlineData(FailureStage.CrmWrite)]
    [InlineData(FailureStage.Search)]
    [InlineData(FailureStage.Activity)]
    [InlineData(FailureStage.CancelledSearch)]
    public async Task Later_failure_retains_the_committed_agent_and_party_without_running_create_rollback(FailureStage stage) {
        var failure = new FailureState(stage);
        var factory = CreateFactory(failure);
        var bridge = new CommittingBridge(factory, failure);
        var search = new Search(failure);
        var activity = new Activity(failure);
        var service = CreateService(factory, bridge, search, activity);

        var exception = await Assert.ThrowsAsync<AiTechnicalAgentCommittedSaveException>(
            () => service.CreateAgentAsync("Retained Agent"));

        Assert.Equal(bridge.AgentId, exception.TechnicalAgentId);
        Assert.Equal(bridge.PartyId, exception.PartyId);
        Assert.Same(failure.Cause, exception.InnerException);
        Assert.Equal(1, bridge.Saves);
        Assert.Equal(0, search.Deletes);
        Assert.DoesNotContain("AiAgentCreationRolledBack", activity.Actions);
        await using var persisted = factory.CreateDbContext();
        Assert.Equal(bridge.PartyId, (await persisted.Set<Party>().SingleAsync()).Id);
        var binding = await persisted.Set<AiResourceBinding>().SingleAsync();
        Assert.Equal(bridge.AgentId, binding.TechnicalAgentId);
        Assert.Equal(bridge.PartyId, binding.PartyId);
        Assert.Equal(bridge.PartyId, (await persisted.Set<AiAgentProfile>().SingleAsync()).PartyId);
    }

    [Fact]
    public async Task Known_pre_save_rejection_keeps_the_existing_create_rollback_behavior() {
        var failure = new FailureState(FailureStage.RejectedBeforeSave);
        var factory = CreateFactory(failure);
        var bridge = new CommittingBridge(factory, failure);
        var search = new Search(failure);
        var activity = new Activity(failure);
        var service = CreateService(factory, bridge, search, activity);

        var result = await service.CreateAgentAsync("Rejected Agent");

        Assert.True(result.IsFailure);
        Assert.Equal(1, bridge.Saves);
        Assert.Equal(2, search.Deletes);
        Assert.Contains("AiAgentCreationRolledBack", activity.Actions);
        await using var persisted = factory.CreateDbContext();
        Assert.Empty(await persisted.Set<Party>().ToArrayAsync());
        Assert.Empty(await persisted.Set<AiResourceBinding>().ToArrayAsync());
        Assert.Empty(await persisted.Set<AiAgentProfile>().ToArrayAsync());
    }

    private static ContextFactory CreateFactory(FailureState failure) => new(
        new DbContextOptionsBuilder<CrmHrDbContext>()
            .UseInMemoryDatabase($"crm-committed-save-{Guid.NewGuid():N}")
            .AddInterceptors(new WriteFailure(failure))
            .Options);

    private static AiAgentService CreateService(ContextFactory factory, CommittingBridge bridge, Search search, Activity activity) {
        var clock = new SystemClock();
        var parties = new PartyDirectoryService(factory, clock, activity, search);
        return new(factory, clock, activity, search, parties, bridge);
    }

    private sealed class FailureState(FailureStage stage) {
        public FailureStage Stage { get; } = stage;
        public bool Armed { get; set; }
        public Exception Cause { get; } = stage == FailureStage.CancelledSearch
            ? new OperationCanceledException("Cancelled after the technical save.")
            : new InvalidOperationException("Injected continuation failure.");

        public void ThrowIf(FailureStage current) {
            if (Armed && Stage == current) {
                throw Cause;
            }
        }
    }

    private sealed class ContextFactory(DbContextOptions<CrmHrDbContext> options) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class WriteFailure(FailureState failure) : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            failure.ThrowIf(FailureStage.CrmWrite);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommittingBridge(ContextFactory factory, FailureState failure) : IAiTechnicalAgentBridge {
        public Guid AgentId { get; } = Guid.NewGuid();
        public Guid PartyId { get; private set; }
        public int Saves { get; private set; }

        public async Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) {
            Saves++;
            PartyId = model.PartyId;
            if (failure.Stage == FailureStage.RejectedBeforeSave) {
                return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation("Rejected before save.", "test.pre-save"));
            }

            await using var context = factory.CreateDbContext();
            context.Add(new AiAgentProfile { PartyId = model.PartyId });
            context.Add(new AiResourceBinding {
                PartyId = model.PartyId, TechnicalAgentId = AgentId, BindingStatus = AiResourceBindingStatus.Bound
            });
            await context.SaveChangesAsync(cancellationToken);
            failure.Armed = true;
            return Result<AiTechnicalAgentSaveResult>.Success(new(AgentId, AiResourceBindingStatus.Bound, "Saved", "/agents"));
        }

        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTechnicalAgentWorkspaceModel(AgentId, AiResourceBindingStatus.Bound, "Saved", "/agents",
                null, "Provider", AiExecutionMode.Remote, "test-model", [], []));

        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
            IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
            IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Search(FailureState failure) : ISearchIndexService {
        public int Deletes { get; private set; }
        public Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) {
            failure.ThrowIf(FailureStage.Search);
            failure.ThrowIf(FailureStage.CancelledSearch);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default) {
            Deletes++;
            return Task.CompletedTask;
        }

        public Task UpsertForMutationAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Activity(FailureState failure) : IActivityStream {
        public List<string> Actions { get; } = [];
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            failure.ThrowIf(FailureStage.Activity);
            Actions.Add(request.Action);
            return Task.CompletedTask;
        }
    }
}
