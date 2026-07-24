using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration;

public sealed class AiAgentDirectoryQueryIntegrationTests
{
    [Fact]
    public async Task Query_filters_and_pages_the_projection_before_technical_enrichment()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("aiagentdirectoryquery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var ownerId = DeterministicGuid(10_000);
        var agentIds = Enumerable.Range(0, 65)
            .Select(index => DeterministicGuid(11_000 + index))
            .ToArray();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().Add(new Party
            {
                Id = ownerId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Directory owner",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch
            });

            for (var index = 0; index < agentIds.Length; index++)
            {
                var partyId = agentIds[index];
                dbContext.Set<Party>().Add(new Party
                {
                    Id = partyId,
                    PartyType = PartyType.AiAgent,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = index == 62
                        ? "Agent 062 needle"
                        : $"Agent {index:D3}",
                    Summary = "Projected technical resource",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(index)
                });
                dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
                {
                    PartyId = partyId,
                    TechnicalAgentId = DeterministicGuid(12_000 + index),
                    BindingStatus = AiResourceBindingStatus.Bound,
                    BindingReason = "Integration test",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
                dbContext.Set<AiAgentProfile>().Add(new AiAgentProfile
                {
                    PartyId = partyId,
                    OwnerPartyId = ownerId,
                    ValidationStatus = index % 2 == 0
                        ? AiValidationStatus.Approved
                        : AiValidationStatus.ReviewRequired
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var bridge = new RecordingTechnicalAgentBridge(agentIds);
        var service = new AiAgentDirectoryQueryService(factory, bridge);
        await service.RefreshProjectionAsync();
        interceptor.Clear();

        var thirdPage = await service.SearchAsync(new AiAgentDirectoryQuery(
            PageIndex: 2,
            PageSize: 20));
        var filtered = await service.SearchAsync(new AiAgentDirectoryQuery(
            SearchText: "needle",
            ValidationStatus: AiValidationStatus.Approved,
            PageSize: 10));

        Assert.Equal(65, thirdPage.TotalCount);
        Assert.Equal(20, thirdPage.Items.Count);
        Assert.Equal(20, bridge.SummaryRequests[0].Count);
        var match = Assert.Single(filtered.Items);
        Assert.Equal(agentIds[62], match.PartyId);
        Assert.Equal("Directory owner", match.OwnerName);
        Assert.Equal(AiValidationStatus.Approved, match.ValidationStatus);
        Assert.Single(bridge.SummaryRequests[1]);
        Assert.Equal(1, bridge.RefreshCount);
        Assert.Contains(
            interceptor.Commands,
            command =>
                command.CommandText.Contains("CrmHr_Parties", StringComparison.Ordinal) &&
                command.CommandText.Contains("CrmHr_AiResourceBindings", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
    }

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private sealed record CapturedCommand(string CommandText);

    private sealed class QueryCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();

        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public void Clear()
        {
            while (commands.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Enqueue(new CapturedCommand(command.CommandText));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }

    private sealed class RecordingTechnicalAgentBridge
        : IAiTechnicalAgentBridge
    {
        private readonly IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary> summaries;

        public RecordingTechnicalAgentBridge(IReadOnlyList<Guid> partyIds)
        {
            summaries = partyIds
                .Select((partyId, index) => new
                {
                    PartyId = partyId,
                    Summary = new AiTechnicalAgentDirectorySummary(
                        DeterministicGuid(12_000 + index),
                        AiResourceBindingStatus.Bound,
                        "Bound",
                        AiExecutionMode.Remote,
                        "Provider",
                        "model",
                        2,
                        true,
                        $"/agents?tab=agents&agentId={DeterministicGuid(12_000 + index):D}")
                })
                .ToDictionary(item => item.PartyId, item => item.Summary);
        }

        public int RefreshCount { get; private set; }

        public List<IReadOnlyList<Guid>> SummaryRequests { get; } = [];

        public Task SynchronizeDirectoryProjectionAsync(
            CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
            IReadOnlyList<Guid> partyIds,
            CancellationToken cancellationToken = default)
        {
            SummaryRequests.Add(partyIds.ToArray());
            IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary> result = partyIds
                .ToDictionary(partyId => partyId, partyId => summaries[partyId]);
            return Task.FromResult(result);
        }

        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
            IReadOnlyList<Guid> partyIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(
            AiAgentProfileEditorModel model,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
