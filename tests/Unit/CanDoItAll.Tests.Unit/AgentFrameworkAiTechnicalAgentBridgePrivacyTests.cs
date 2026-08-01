using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AgentFrameworkAiTechnicalAgentBridgePrivacyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-24T12:00:00Z");

    [Fact]
    public async Task Synchronization_does_not_project_runtime_agent_tags_into_party_business_tags()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(CrmHrModuleAssemblyMarker).Assembly,
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var existingPartyId = Guid.Parse("7b3eeb93-5b7f-47cc-888f-058fb441d15b");
        var newPartyId = Guid.Parse("b86c540f-048b-4774-a7ca-7d9c70053176");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"agent-framework-party-tags-{Guid.NewGuid():N}")
            .Options;
        IDbContextFactory<AppDbContext> dbContextFactory = new TestDbContextFactory(options);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<Party>().Add(new Party
            {
                Id = existingPartyId,
                PartyType = PartyType.AiAgent,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Existing technical party",
                TagsJson = """["business-owned"]""",
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
            await dbContext.SaveChangesAsync();
        }

        var agents = new[]
        {
            CreateAgent(existingPartyId, "Existing runtime agent", "runtime-existing"),
            CreateAgent(newPartyId, "New runtime agent", "runtime-new")
        };
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        ((WorkspaceServiceProxy)(object)workspace).Agents = agents;
        var bridge = CreateBridge(
            dbContextFactory,
            new StubWorkspaceFactory(workspace),
            new FixedClock(Now));

        await bridge.SynchronizeDirectoryProjectionAsync();

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var existingParty = await verificationContext.Set<Party>()
            .SingleAsync(party => party.Id == existingPartyId);
        var newParty = await verificationContext.Set<Party>()
            .SingleAsync(party => party.Id == newPartyId);

        Assert.Equal(
            new[] { "business-owned" },
            JsonSerializer.Deserialize<List<string>>(existingParty.TagsJson));
        Assert.Empty(JsonSerializer.Deserialize<List<string>>(newParty.TagsJson) ?? []);
    }

    [Fact]
    public async Task Directory_summary_reads_the_persisted_projection_without_enumerating_the_technical_catalog()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(CrmHrModuleAssemblyMarker).Assembly,
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var partyId = Guid.Parse("83bcd274-eeb1-4457-b809-7c6f3388b27c");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"agent-framework-summary-projection-{Guid.NewGuid():N}")
            .Options;
        IDbContextFactory<AppDbContext> dbContextFactory = new TestDbContextFactory(options);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var workspaceProxy = (WorkspaceServiceProxy)(object)workspace;
        workspaceProxy.Agents =
        [
            CreateAgent(partyId, "Projected runtime agent", "runtime-projection")
        ];
        var bridge = CreateBridge(
            dbContextFactory,
            new StubWorkspaceFactory(workspace),
            new FixedClock(Now));

        await bridge.SynchronizeDirectoryProjectionAsync();
        workspaceProxy.ResetCatalogCallCounts();

        var summaries = await bridge.GetDirectorySummariesAsync(
            [partyId, partyId, Guid.Empty]);
        var staffingFacts = await bridge.GetStaffingFactsAsync([partyId]);

        var summary = Assert.Single(summaries).Value;
        var staffingFact = Assert.Single(staffingFacts).Value;
        Assert.True(summary.HasTechnicalProfile);
        Assert.Equal(AiResourceBindingStatus.Bound, summary.BindingStatus);
        Assert.Equal("Technical agent", staffingFact.RoleTitle);
        Assert.Equal("Execute technical work.", staffingFact.Instructions);
        Assert.Contains("runtime-projection", staffingFact.Tags);
        Assert.Equal(0, workspaceProxy.ListAgentsCallCount);
        Assert.Equal(0, workspaceProxy.ListProvidersCallCount);
        Assert.Equal(0, workspaceProxy.ListCapabilitiesCallCount);
    }

    private static IAiTechnicalAgentBridge CreateBridge(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IClock clock)
    {
        var implementationType = typeof(AgentFrameworkModuleServiceCollectionExtensions).Assembly.GetType(
            "CanDoItAll.Modules.AgentFramework.AgentFrameworkAiTechnicalAgentBridge",
            throwOnError: true)!;
        var constructor = Assert.Single(implementationType.GetConstructors());
        return Assert.IsAssignableFrom<IAiTechnicalAgentBridge>(
            constructor.Invoke([dbContextFactory, workspaceFactory, clock]));
    }

    private static AgentDefinition CreateAgent(
        Guid partyId,
        string name,
        string runtimeTag)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            "Technical agent",
            "Runtime projection",
            "Execute technical work.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: JsonSerializer.Serialize(new
            {
                crmHr = new
                {
                    partyId
                }
            }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags:
            [
                "crm-hr",
                $"party-{partyId:N}",
                runtimeTag
            ],
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }

    private sealed class StubWorkspaceFactory(
        IAgentFrameworkWorkspaceService service) : ICanDoItAllAgentWorkspaceFactory
    {
        private readonly WorkspaceScopeDescriptor scope = WorkspaceScopeDescriptor.Organization("privacy-tests");

        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            return service;
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor requestedScope)
        {
            return service;
        }

        public WorkspaceScopeDescriptor GetOrganizationScope()
        {
            return scope;
        }

        public string GetWorkspaceRoot()
        {
            return string.Empty;
        }
    }

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public int ListAgentsCallCount { get; private set; }

        public int ListProvidersCallCount { get; private set; }

        public int ListCapabilitiesCallCount { get; private set; }

        public void ResetCatalogCallCounts()
        {
            ListAgentsCallCount = 0;
            ListProvidersCallCount = 0;
            ListCapabilitiesCallCount = 0;
        }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync))
            {
                ListAgentsCallCount++;
                return Task.FromResult(Agents);
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListProvidersAsync))
            {
                ListProvidersCallCount++;
                return Task.FromResult<IReadOnlyList<ProviderProfile>>([]);
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync))
            {
                ListCapabilitiesCallCount++;
                return Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]);
            }

            throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
