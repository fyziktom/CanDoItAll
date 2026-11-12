using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessParticipant(IProcessDecisionChildReader children,
    IProcessDecisionMaterialReader materials, ProjectTaskPlanProcessReadAuthority read,
    ProjectTaskPlanProcessBinding binding) : IAgentInheritedPlanningInitialBinder {
    public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
    public AgentPlanningPromptContract Prompt => ProjectTaskPlanPlanningContent.Prompt;

    public bool SupportsParent(AgentToolProtocolEnvelope parent) => ProjectTaskPlanProcessBinding.Supports(parent);

    public Task<AgentInheritedPlanningBinding> BindInitialAsync(AgentInheritedPlanningBindingContext context,
        AgentToolProtocolEnvelope originalParent, AgentToolProtocolEnvelope sourceEvidence, CancellationToken cancellationToken)
        => binding.BindAsync(context, originalParent, sourceEvidence, cancellationToken);

    public async Task<AgentPlanningBasisRecord> ReadOriginalBasisAsync(AgentPlanningInheritedOrigin original,
        CancellationToken cancellationToken) {
        var parent = ProjectTaskPlanProcessCodec.Parent(original);
        var child = await children.ReadAsync(parent.Child.Address, cancellationToken);
        if (child?.Admission != parent.Child) {
            throw ProjectTaskPlanAgentCodec.Denied("The original task-plan child or its allocation is unavailable.");
        }
        var material = await materials.ReadAsync(parent.Scope, parent.Material.Reference, cancellationToken)
            ?? throw ProjectTaskPlanAgentCodec.Denied("The original task-plan material is unavailable.");
        return ProjectTaskPlanProcessCodec.Basis(original, material);
    }

    public Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken)
        => read.RequireAsync(decision, cancellationToken);

    public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision,
        AgentPlanningBasisRecord basis, string responseText)
        => ProjectTaskPlanPlanningContent.Prepare(() => ProjectTaskPlanProcessCodec.Prepare(decision, basis, responseText));

    public string DescribePublication(AgentToolSessionObservation original, AgentToolPreparedPayload payload,
        AgentToolApprovalBinding binding)
        => ProjectTaskPlanPlanningContent.Describe(Resolve(original, payload, binding));

    public AgentPlanningPublicationAssessment ClassifyPublication(AgentToolSessionObservation original,
        AgentToolPreparedPayload payload, AgentSavedEffectDispatchResult result)
        => ProjectTaskPlanAgentResult.Classify(Resolve(original, payload, result.Binding), result);

    private static ProjectTaskPlanCommand Resolve(AgentToolSessionObservation original, AgentToolPreparedPayload payload,
        AgentToolApprovalBinding binding) => ProjectTaskPlanProcessCodec.Resolve(
            original.Planning ?? throw ProjectTaskPlanAgentCodec.Denied("The original inherited planning observation is missing."),
            original.Session, payload, binding).Command;
}
