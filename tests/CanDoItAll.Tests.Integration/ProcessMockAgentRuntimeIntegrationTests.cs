using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessMockAgentRuntimeIntegrationTests
{
    [Fact]
    public async Task Process_mock_catalog_is_not_seeded_when_disabled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();

        var context = await catalogService.EnsureCatalogAsync();

        Assert.Null(context);

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.DoesNotContain(providers, ProcessMockAgentCatalog.IsProcessMockProvider);
        Assert.DoesNotContain(
            agents,
            agent => agent.Tags.Contains(ProcessMockAgentCatalog.AgentTag, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Process_mock_catalog_seeds_role_agents_when_enabled()
    {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();

        var context = await catalogService.EnsureCatalogAsync();

        Assert.NotNull(context);
        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, context.AgentIdsByRoleKey.Count);

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var provider = Assert.Single(
            await workspaceService.ListProvidersAsync(),
            ProcessMockAgentCatalog.IsProcessMockProvider);

        Assert.True(provider.IsEnabled);
        Assert.Equal(ProcessMockAgentCatalog.Model, provider.DefaultModel);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var roleTag = ProcessMockAgentCatalog.CreateRoleTag(role.RoleKey);
            var agent = Assert.Single(
                agents,
                item => item.ProviderProfileId == provider.Id &&
                        item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase));

            Assert.Equal(AgentLifecycleStatus.Active, agent.Status);
            Assert.Contains(agent.Tags, item => string.Equals(item, ProcessMockAgentCatalog.AgentTag, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(ProcessMockAgentCatalog.Model, agent.Model);
        }

        var technicalAgentBridge = scope.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>();
        var partyIds = ProcessMockAgentCatalog.Roles.Select(item => item.PartyId).ToList();
        var staffingFacts = await technicalAgentBridge.GetStaffingFactsAsync(partyIds);

        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, staffingFacts.Count);
        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var fact = staffingFacts[role.PartyId];
            Assert.Equal(AiResourceBindingStatus.Bound, fact.BindingStatus);
            Assert.True(fact.TechnicalAgentId.HasValue);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, fact.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, fact.DefaultModel);
        }
    }

    [Fact]
    public async Task Process_mock_runtime_runs_deterministic_calculator_rejection_repair_and_approval()
    {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        await catalogService.EnsureCatalogAsync();

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var qaAgent = FindRoleAgent(agents, ProcessMockAgentRoleKeys.Qa);
        var repairAgent = FindRoleAgent(agents, ProcessMockAgentRoleKeys.RepairDeveloper);

        const string processRunId = "mock-run-001";

        var qaRejection = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: qaAgent.Id,
            Prompt: "Run process mock QA first pass for the calculator implementation.",
            Context: CreateProcessContext("qa-first-pass", processRunId, "qa-first-pass")));
        Assert.Contains(ProcessMockAgentCatalog.BranchRepairsRequired, qaRejection.ResponseText, StringComparison.Ordinal);

        var rejectionDetail = await workspaceService.GetExecutionRunDetailAsync(qaRejection.ExecutionRunId);
        Assert.Contains(
            rejectionDetail.Artifacts,
            artifact => artifact.RelativePath.EndsWith("/04-qa-finding.md", StringComparison.OrdinalIgnoreCase));

        var repair = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: repairAgent.Id,
            Prompt: "Run process mock repair developer step for the calculator implementation.",
            Context: CreateProcessContext("repair", processRunId, "repair")));
        Assert.Contains("PROCESS_STEP_OUTCOME", repair.ResponseText, StringComparison.Ordinal);

        var fileService = new WorkspaceFileService(workspaceFactory.GetWorkspaceRoot(), workspaceFactory.GetOrganizationScope());
        var repairedEngine = fileService.ReadTextFile("output/process-mock/mockrun001/CalculatorApp/CalculatorEngine.cs", 8000);
        Assert.True(repairedEngine.Succeeded, repairedEngine.Message);
        Assert.Contains("throw new DivideByZeroException", repairedEngine.Content, StringComparison.Ordinal);

        var qaApproval = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: qaAgent.Id,
            Prompt: "Run process mock QA approval for the repaired calculator implementation.",
            Context: CreateProcessContext("qa-approval", processRunId, "qa-approval")));
        Assert.Contains(ProcessMockAgentCatalog.BranchApproved, qaApproval.ResponseText, StringComparison.Ordinal);

        var approvalDetail = await workspaceService.GetExecutionRunDetailAsync(qaApproval.ExecutionRunId);
        Assert.Contains(
            approvalDetail.Artifacts,
            artifact => artifact.RelativePath.EndsWith("/06-qa-approval.md", StringComparison.OrdinalIgnoreCase));
    }

    private static Task<TestApplication> CreateEnabledApplicationAsync()
    {
        return TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                [$"{ProcessMockAgentOptions.SectionName}:Enabled"] = "true"
            }
        });
    }

    private static AgentDefinition FindRoleAgent(
        IReadOnlyList<AgentDefinition> agents,
        string roleKey)
    {
        var roleTag = ProcessMockAgentCatalog.CreateRoleTag(roleKey);
        return Assert.Single(
            agents,
            item => item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase));
    }

    private static ExecutionInvocationContext CreateProcessContext(
        string sourceId,
        string processRunId,
        string processStepId)
    {
        return new ExecutionInvocationContext(
            SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
            SourceId: sourceId,
            CorrelationId: $"{processRunId}-{processStepId}",
            CausationId: processRunId,
            RequestedBy: "process-mock-tests",
            RequestedByKind: "test",
            MetadataJson: "{}",
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);
    }
}
