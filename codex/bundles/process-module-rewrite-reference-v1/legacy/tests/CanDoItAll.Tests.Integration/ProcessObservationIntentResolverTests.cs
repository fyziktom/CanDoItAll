using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessObservationIntentResolverTests
{
    [Fact]
    public async Task ResolveAsync_requires_definition_or_run_for_focused_details()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IProcessObservationIntentResolver>();

        var plan = await resolver.ResolveAsync(new ProcessObservationIntent(
            ProjectId: null,
            ProcessDefinitionId: null,
            ProcessRunId: null,
            StepRunId: null,
            FocusKind: ProcessObservationFocusKind.QualityReview));

        Assert.Equal(ProcessObservationIntentResolutionStatus.Ambiguous, plan.Status);
        Assert.Empty(plan.DialogDescriptors);
    }

    [Fact]
    public void ProcessManagerAgentResolver_returns_reason_code_for_selected_run_assignment()
    {
        var assignedManagerPartyId = Guid.NewGuid();
        var assignedManagerTechnicalAgentId = Guid.NewGuid();

        var result = ProcessManagerAgentResolver.ResolveAssignedManager(
            [
                CreateAssignment(assignedManagerPartyId, "Delivery manager AI agent", "Delivery manager"),
                CreateAssignment(Guid.NewGuid(), "Implementation developer", "Implementation engineer")
            ],
            [
                new ProcessManagerAgentOption(
                    assignedManagerPartyId,
                    assignedManagerTechnicalAgentId,
                    "Delivery manager AI agent",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.True(result.IsResolved);
        Assert.Equal(assignedManagerTechnicalAgentId, result.ResolvedTechnicalAgentId);
        Assert.Equal(ProcessManagerAgentResolutionReasonCode.SelectedRunAssignment, result.ReasonCode);
        Assert.InRange(result.Confidence, 1, 100);
        Assert.Contains("Selected-run assignment", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessManagerAgentResolver_reports_ambiguous_selected_run_assignments()
    {
        var firstManagerPartyId = Guid.NewGuid();
        var secondManagerPartyId = Guid.NewGuid();

        var result = ProcessManagerAgentResolver.ResolveAssignedManager(
            [
                CreateAssignment(firstManagerPartyId, "Delivery manager east", "Process manager"),
                CreateAssignment(secondManagerPartyId, "Delivery manager west", "Process manager")
            ],
            [
                new ProcessManagerAgentOption(
                    firstManagerPartyId,
                    Guid.NewGuid(),
                    "Delivery manager east",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog."),
                new ProcessManagerAgentOption(
                    secondManagerPartyId,
                    Guid.NewGuid(),
                    "Delivery manager west",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.False(result.IsResolved);
        Assert.True(result.IsAmbiguous);
        Assert.Equal(ProcessManagerAgentResolutionReasonCode.AmbiguousSelectedRunAssignment, result.ReasonCode);
        Assert.Contains("Delivery manager east", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Delivery manager west", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessManagerAgentResolver_prefers_manager_capability_over_text_signal()
    {
        var capabilityBackedAgent = CreateAgentDefinition(
            "Delivery coordinator",
            "Operations support",
            [CreateCapability("process-manager-chat")]);
        var textOnlyAgent = CreateAgentDefinition(
            "Delivery Manager",
            "General management",
            []);

        var result = ProcessManagerAgentResolver.ResolveFallbackManager(
            [],
            [textOnlyAgent, capabilityBackedAgent]);

        Assert.True(result.IsResolved);
        Assert.Equal(capabilityBackedAgent.Id, result.ResolvedTechnicalAgentId);
        Assert.Equal(ProcessManagerAgentResolutionReasonCode.FallbackAgentCapability, result.ReasonCode);
        Assert.Equal(100, result.Confidence);
    }

    [Fact]
    public void ProcessManagerAgentResolver_blocks_ambiguous_fallback_manager_options()
    {
        var result = ProcessManagerAgentResolver.ResolveFallbackManager(
            [
                new ProcessManagerAgentOption(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Process manager",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog."),
                new ProcessManagerAgentOption(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Process manager",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.False(result.IsResolved);
        Assert.True(result.IsAmbiguous);
        Assert.Equal(ProcessManagerAgentResolutionReasonCode.AmbiguousFallbackManager, result.ReasonCode);
        Assert.Contains("select or configure", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessRunAssignmentViewModel CreateAssignment(
        Guid partyId,
        string displayName,
        string roleDisplayName)
    {
        return new ProcessRunAssignmentViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StepDefinitionId: null,
            partyId,
            WorkflowDefinitionId: null,
            WorkflowVersionId: null,
            displayName,
            "technical-agent",
            "Assigned from selected run staffing.",
            "test",
            "Test assignment.",
            IsFallback: false,
            IsCapabilityGap: false,
            AllowsDirectMessaging: true)
        {
            RoleDisplayName = roleDisplayName
        };
    }

    private static AgentDefinition CreateAgentDefinition(
        string name,
        string roleTitle,
        IReadOnlyList<AgentCapabilityAssignment> capabilities)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            roleTitle,
            "Test manager agent.",
            "Manage the process.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            capabilities,
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static AgentCapabilityAssignment CreateCapability(string capabilityKey)
    {
        return new AgentCapabilityAssignment(
            Guid.NewGuid(),
            capabilityKey,
            CapabilityKind.Skill,
            CapabilityProofStatus.Verified,
            DateTimeOffset.UtcNow,
            "Test capability.");
    }
}
