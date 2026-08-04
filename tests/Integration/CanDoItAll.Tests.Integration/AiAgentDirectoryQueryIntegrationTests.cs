using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
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
    public async Task Query_rejects_multiple_parties_bound_to_the_same_technical_agent()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("aiagentdirectoryduplicate");
        var factory = new TestDbContextFactory(database.CreateAppDbContextOptions());
        var technicalAgentId = DeterministicGuid(9_000);
        var firstPartyId = DeterministicGuid(9_001);
        var secondPartyId = DeterministicGuid(9_002);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            foreach (var partyId in new[] { firstPartyId, secondPartyId })
            {
                dbContext.Set<Party>().Add(new Party
                {
                    Id = partyId,
                    PartyType = PartyType.AiAgent,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = $"Duplicate projection {partyId:D}",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
                dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
                {
                    PartyId = partyId,
                    TechnicalAgentId = technicalAgentId,
                    BindingStatus = AiResourceBindingStatus.Bound,
                    BindingReason = "Invalid duplicate binding",
                    ProjectionUpdatedAtUtc = DateTimeOffset.UnixEpoch,
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var agent = CreateAgent(technicalAgentId, DeterministicGuid(9_100), 0);
        var referenceDataProvider = new RecordingReferenceDataProvider(
            new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [agent],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UnixEpoch,
                TimeSpan.Zero));
        using var service = new AiAgentDirectoryQueryService(
            factory,
            new RecordingTechnicalAgentBridge(),
            referenceDataProvider,
            new AgentReferenceDataInvalidationHub());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchAsync(new AiAgentDirectoryQuery()));

        Assert.Contains(technicalAgentId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Contains(firstPartyId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Contains(secondPartyId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Query_reuses_composite_snapshot_until_reference_data_is_invalidated()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("aiagentdirectoryquery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var ownerId = DeterministicGuid(10_000);
        var providerId = DeterministicGuid(10_500);
        var partyIds = Enumerable.Range(0, 65)
            .Select(index => DeterministicGuid(11_000 + index))
            .ToArray();
        var technicalAgentIds = Enumerable.Range(0, partyIds.Length)
            .Select(index => DeterministicGuid(12_000 + index))
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

            for (var index = 0; index < partyIds.Length; index++)
            {
                var partyId = partyIds[index];
                dbContext.Set<Party>().Add(new Party
                {
                    Id = partyId,
                    PartyType = PartyType.AiAgent,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = $"CRM projection {index:D3}",
                    Summary = "This flattened CRM text must not drive technical search.",
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(index)
                });
                dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
                {
                    PartyId = partyId,
                    TechnicalAgentId = technicalAgentIds[index],
                    BindingStatus = AiResourceBindingStatus.Bound,
                    BindingReason = "Integration test",
                    ProjectedExecutionMode = AiExecutionMode.Remote,
                    ProjectionUpdatedAtUtc = DateTimeOffset.UnixEpoch.AddHours(index),
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

        var agents = technicalAgentIds
            .Select((technicalAgentId, index) =>
                CreateAgent(technicalAgentId, providerId, index))
            .ToArray();
        var provider = CreateProvider(providerId);
        var referenceDataProvider = new RecordingReferenceDataProvider(
            new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents | AgentReferenceDataSections.Providers,
                agents,
                [provider],
                new Dictionary<Guid, ProviderProfile>
                {
                    [provider.Id] = provider
                },
                DateTimeOffset.UnixEpoch,
                TimeSpan.Zero));
        var bridge = new RecordingTechnicalAgentBridge();
        var invalidator = new AgentReferenceDataInvalidationHub();
        using var service = new AiAgentDirectoryQueryService(
            factory,
            bridge,
            referenceDataProvider,
            invalidator);
        await service.RefreshProjectionAsync();
        interceptor.Clear();

        var thirdPage = await service.SearchAsync(new AiAgentDirectoryQuery(
            PageIndex: 2,
            PageSize: 20));
        var filtered = await service.SearchAsync(new AiAgentDirectoryQuery(
            SearchText: "needle",
            ValidationStatus: AiValidationStatus.Approved,
            PageSize: 10));
        var deepLinkedItem = await service.GetByPartyIdAsync(partyIds[0]);

        Assert.Equal(65, thirdPage.TotalCount);
        Assert.Equal(20, thirdPage.Items.Count);
        var match = Assert.Single(filtered.Items);
        Assert.Equal(partyIds[62], match.PartyId);
        Assert.Equal(technicalAgentIds[62], match.Agent.Id);
        Assert.Equal("Directory owner", match.Governance.OwnerName);
        Assert.Equal(AiValidationStatus.Approved, match.Governance.ValidationStatus);
        Assert.Same(
            referenceDataProvider.Snapshot.ProviderById[provider.Id],
            match.Provider);
        Assert.True(match.IsPrivateProvider);
        Assert.NotNull(deepLinkedItem);
        Assert.Equal(technicalAgentIds[0], deepLinkedItem.Agent.Id);
        Assert.Single(referenceDataProvider.Requests);
        Assert.Equal(1, CountProjectionSnapshotQueries(interceptor));

        invalidator.Invalidate();
        var afterExternalInvalidation = await service.SearchAsync(new AiAgentDirectoryQuery(
            SearchText: "needle",
            PageSize: 10));

        Assert.Single(afterExternalInvalidation.Items);
        Assert.Equal(2, referenceDataProvider.Requests.Count);
        Assert.Equal(2, CountProjectionSnapshotQueries(interceptor));

        await service.RefreshProjectionAsync();
        await service.GetByPartyIdAsync(partyIds[62]);

        Assert.Equal(2, bridge.RefreshCount);
        Assert.Equal(3, referenceDataProvider.Requests.Count);
        Assert.Equal(3, CountProjectionSnapshotQueries(interceptor));
        Assert.All(
            referenceDataProvider.Requests,
            request => Assert.Equal(
                AgentReferenceDataRequest.AgentsAndProviders(false),
                request));
    }

    private static int CountProjectionSnapshotQueries(QueryCommandInterceptor interceptor)
    {
        return interceptor.Commands.Count(command =>
            command.CommandText.Contains(
                "CrmHr_AiResourceBindings",
                StringComparison.Ordinal) &&
            command.CommandText.Contains(
                "CrmHr_Parties",
                StringComparison.Ordinal));
    }

    private static AgentDefinition CreateAgent(
        Guid id,
        Guid providerId,
        int index)
    {
        return new AgentDefinition(
            id,
            index == 62
                ? "Agent 062 needle"
                : $"Agent {index:D3}",
            index == 62
                ? "Searchable specialist"
                : "Integration specialist",
            "Canonical technical resource",
            "Use integration-test instructions.",
            AgentLifecycleStatus.Active,
            providerId,
            "integration-model",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            false,
            false,
            "{}",
            false,
            string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            index == 62
                ? ["integration", "needle-tag"]
                : ["integration"],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(index));
    }

    private static ProviderProfile CreateProvider(Guid id)
    {
        return new ProviderProfile(
            id,
            "Private integration provider",
            ProviderKind.OpenAi,
            "https://example.invalid",
            "INTEGRATION_API_KEY",
            "integration-model",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            true,
            false,
            "{}",
            string.Empty,
            "Healthy",
            DateTimeOffset.UnixEpoch,
            ["integration-model"])
        {
            IsPrivateProvider = true
        };
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

    private sealed class RecordingReferenceDataProvider(
        AgentReferenceDataSnapshot snapshot) : IAgentReferenceDataProvider
    {
        public AgentReferenceDataSnapshot Snapshot { get; } = snapshot;

        public List<AgentReferenceDataRequest> Requests { get; } = [];

        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class RecordingTechnicalAgentBridge : IAiTechnicalAgentBridge
    {
        public int RefreshCount { get; private set; }

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
            throw new NotSupportedException();
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
