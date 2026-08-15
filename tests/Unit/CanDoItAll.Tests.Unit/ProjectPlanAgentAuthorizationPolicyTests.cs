using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectPlanAgentAuthorizationPolicyTests
{
    [Fact]
    public void IsPlanSummaryAuthorized_requires_exact_skill_and_tool_assignments()
    {
        var skill = CreateCapability(ProjectPlanAgentCapabilityKeys.AnalysisSkill, CapabilityKind.Skill);
        var tool = CreateCapability(ProjectPlanAgentCapabilityKeys.SummaryTool, CapabilityKind.Tool);
        var agent = CreateAgent(skill, tool);

        var authorized = ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(agent, [skill, tool]);

        Assert.True(authorized);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void IsPlanSummaryAuthorized_rejects_missing_required_capability(
        bool includeSkill,
        bool includeTool)
    {
        var skill = CreateCapability(ProjectPlanAgentCapabilityKeys.AnalysisSkill, CapabilityKind.Skill);
        var tool = CreateCapability(ProjectPlanAgentCapabilityKeys.SummaryTool, CapabilityKind.Tool);
        var assignments = new List<CapabilityCatalogItem>();
        if (includeSkill)
        {
            assignments.Add(skill);
        }
        if (includeTool)
        {
            assignments.Add(tool);
        }

        var agent = CreateAgent(assignments.ToArray());

        Assert.False(ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(agent, [skill, tool]));
    }

    [Fact]
    public void IsPlanSummaryAuthorized_rejects_stale_or_duplicate_exact_assignments()
    {
        var skill = CreateCapability(ProjectPlanAgentCapabilityKeys.AnalysisSkill, CapabilityKind.Skill);
        var tool = CreateCapability(ProjectPlanAgentCapabilityKeys.SummaryTool, CapabilityKind.Tool);
        var staleToolAssignment = CreateAssignment(tool) with { CapabilityId = Guid.NewGuid() };
        var staleAgent = CreateAgent([CreateAssignment(skill), staleToolAssignment]);
        var duplicateAgent = CreateAgent(
            [CreateAssignment(skill), CreateAssignment(skill), CreateAssignment(tool)]);

        Assert.False(ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(staleAgent, [skill, tool]));
        Assert.False(ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(duplicateAgent, [skill, tool]));
    }

    [Theory]
    [InlineData(AgentLifecycleStatus.Suspended, false, true, false, false)]
    [InlineData(AgentLifecycleStatus.Active, true, true, false, false)]
    [InlineData(AgentLifecycleStatus.Active, false, false, false, false)]
    [InlineData(AgentLifecycleStatus.Active, false, true, true, false)]
    [InlineData(AgentLifecycleStatus.Active, false, true, false, true)]
    public void IsPlanSummaryAuthorized_rejects_invalid_actor_or_capability_identity(
        AgentLifecycleStatus status,
        bool isTemplate,
        bool canUseTools,
        bool useWrongSkillKind,
        bool useWrongSkillKey)
    {
        var skill = CreateCapability(
            useWrongSkillKey ? "different-analysis-skill" : ProjectPlanAgentCapabilityKeys.AnalysisSkill,
            useWrongSkillKind ? CapabilityKind.Tool : CapabilityKind.Skill);
        var tool = CreateCapability(ProjectPlanAgentCapabilityKeys.SummaryTool, CapabilityKind.Tool);
        var agent = CreateAgent(skill, tool) with
        {
            Status = status,
            IsTemplate = isTemplate,
            Permissions = AgentPermissionsPolicy.Default with { CanUseTools = canUseTools }
        };

        Assert.False(ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(agent, [skill, tool]));
    }

    private static CapabilityCatalogItem CreateCapability(string key, CapabilityKind kind)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            kind,
            key,
            key,
            key,
            EndpointOrPath: string.Empty,
            ConfigurationJson: string.Empty,
            CapabilityProofStatus.Verified,
            ProofNotes: string.Empty,
            LastVerifiedAtUtc: AsOfUtc,
            IsBuiltIn: true);
    }

    private static AgentDefinition CreateAgent(params CapabilityCatalogItem[] capabilities)
    {
        return CreateAgent(capabilities.Select(CreateAssignment).ToArray());
    }

    private static AgentDefinition CreateAgent(IReadOnlyList<AgentCapabilityAssignment> assignments)
    {
        return new AgentDefinition(
            Guid.Parse("fa0f9af3-5292-4e17-a662-824b63ecc2aa"),
            "Planning analyst",
            "Planning analyst",
            "Analyzes project plans.",
            "Use planning tools.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            assignments,
            Tags: [],
            CreatedAtUtc: AsOfUtc,
            UpdatedAtUtc: AsOfUtc);
    }

    private static AgentCapabilityAssignment CreateAssignment(CapabilityCatalogItem capability)
    {
        return new AgentCapabilityAssignment(
            capability.Id,
            capability.Key,
            capability.Kind,
            capability.ProofStatus,
            capability.LastVerifiedAtUtc,
            capability.ProofNotes);
    }

    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
}
