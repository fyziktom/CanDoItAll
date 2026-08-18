using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.AgentContext;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Unit.Projects;

/// <summary>
/// Durable project-structure operational guidance is supplied by the
/// registered runtime contributor for project-structure-sourced runs and no
/// longer lives inside the volatile UI context fragment.
/// </summary>
public sealed class ProjectStructureRuntimeGuidanceContributorTests
{
    [Fact]
    public async Task Guidance_is_provided_for_project_structure_sourced_runs()
    {
        var contributor = new ProjectStructureRuntimeGuidanceContributor();

        var result = await contributor.ContributeAsync(CreateRequest(
            sourceKind: AgentChatTrustedSourceKinds.ProjectStructure));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal(AgentContextMessageRole.System, message.Role);
        Assert.Contains("Selected-node operation contract:", message.Text, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", message.Text, StringComparison.Ordinal);
        Assert.Contains("do not grant workspace access", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guidance_is_skipped_for_other_source_kinds()
    {
        var contributor = new ProjectStructureRuntimeGuidanceContributor();

        var processResult = await contributor.ContributeAsync(CreateRequest(sourceKind: "process-step"));
        var emptyResult = await contributor.ContributeAsync(CreateRequest(sourceKind: string.Empty));

        Assert.Equal(AgentContextContributionStatus.Skipped, processResult.Status);
        Assert.Empty(processResult.Messages);
        Assert.Equal(AgentContextContributionStatus.Skipped, emptyResult.Status);
    }

    [Fact]
    public void Ui_base_fragment_no_longer_carries_the_operational_guidance()
    {
        var fragment = ProjectStructureAgentChatContextBuilder.BuildBaseFragment(Guid.NewGuid());

        Assert.DoesNotContain("Selected-node operation contract", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("project_structure_asset_create", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("project structure", fragment.Content, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentContextContributionRequest CreateRequest(string sourceKind)
    {
        var now = DateTimeOffset.UtcNow;
        var agent = new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Guidance test agent",
            RoleTitle: "Assistant",
            Summary: "Guidance contributor test agent.",
            Instructions: "Test.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: "test-model",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "Test provider",
            ProviderKind.OpenAi,
            "http://provider.test",
            string.Empty,
            "test-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
        return new AgentContextContributionRequest(
            agent,
            provider,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "Hello")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Sandbox))
        {
            ContextIntent = AgentRuntimeContextIntent.Empty with { SourceKind = sourceKind }
        };
    }
}
