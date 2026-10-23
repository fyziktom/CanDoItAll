using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessLaunchPolicyProvider(WorkspaceSettingsWriter settings,
    IAgentCatalogReadLeaseStore catalog, ProjectProcessLaunchAuthorityService sources,
    ProjectWriteAdmissionService projects, ICanonicalRuntimeDatabase database) : IProcessDecisionLaunchPolicyProvider {
    internal static DecisionDataPolicyCapabilities Capabilities { get; } = new(true, true, false, true, true, false,
        PublicationFreshness.RequireCurrent, new(0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));

    internal static ProcessLaunchDecisionDescriptor SupportedContract { get; } = new(ProjectTaskPlanProcessContract.Execution,
        "Reviewed Project task planning",
        "One Agent activity proposes Project tasks for review. Requires a saved Workspace policy, current task authority and explicit task publication permission. Additional activities, branches, subprocesses, tools or artifacts are not supported.",
        Array.AsReadOnly(new[] {
            new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview, Capabilities.BudgetCeiling),
            new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview, Capabilities.BudgetCeiling),
            new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Fail, Capabilities.BudgetCeiling),
            new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Fail, Capabilities.BudgetCeiling)
        }));

    public ProcessLaunchDecisionDescriptor Descriptor => SupportedContract;
    private ProcessDecisionExecutionContract Contract => Descriptor.Execution;

    public async Task<ProcessDecisionLaunchAdmissions> PrepareAsync(ProcessLaunchRequest request,
        ProcessRuntimeCommitRequest initial, CancellationToken cancellationToken) {
        ProcessDecisionLaunchAdmissionCoordinator.RequireRequest(request);
        var selected = request.Decision ?? throw new InvalidDataException("Task planning requires an explicit execution selection.");
        if (selected.Execution != Contract) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedContract);
        }
        var assignment = RequireSingleActivity(initial);
        if (!ProcessLaunchExecutorKinds.CanResolveAsAgent(assignment.ExecutorKind) || !Guid.TryParse(assignment.ExecutorId, out var agentId)) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedPlan);
        }
        var authority = request.Authority!;
        var project = authority.ProjectAdmission!;
        var profile = database.Profile.Profile.Id;
        var generation = database.Generation;
        void RequireProfile() {
            if (database.Profile.Profile.Id != profile || database.Generation != generation || profile != authority.DatabaseProfileId) {
                throw new ProcessLaunchAuthorityRejectedException("The Workspace database profile changed during Process planning preparation.");
            }
        }
        RequireProfile();
        sources.RequireHeldSource(authority, null, mutation: true);
        var captured = await settings.ReadV2Async(cancellationToken).ConfigureAwait(false);
        if (captured.DecisionDataPolicy is not { } workspace || captured.Expectation.Head is not { } head) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.MissingWorkspacePolicy);
        }
        var policy = ResolvePolicy(workspace, selected.Policy);
        await using var held = await catalog.AcquireAgentReadLeaseAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (held.Scope != WorkspaceScopeDescriptor.Organization(profile.ToString("N")) || !held.Revision.IsAssigned ||
                held.Agent is not { IsTemplate: false, Status: AgentLifecycleStatus.Active, Permissions.CanUseTools: true } agent ||
                agent.Id != agentId || !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson))) {
            throw new ProcessLaunchAuthorityRejectedException("The selected Agent does not currently permit Project task planning.");
        }
        await projects.RequireCurrentAsync(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), cancellationToken).ConfigureAwait(false);
        sources.RequireHeldSource(authority, held, mutation: true);
        RequireProfile();
        var definition = initial.InitialPlan!.Definition.VersionId;
        ProcessDecisionRunAdmission root = new(initial.Mutation.State.RunId, new(Guid.NewGuid()), profile,
            captured.Expectation.Namespace.Value, head.Revision, definition, workspace, null, selected.Policy, Capabilities, [], policy);
        if (policy.ReadBasisMode == ReadBasisMode.RunSnapshot) {
            root = root with { MaterialScope = new(new(Guid.NewGuid()), root.Funding, profile, root.WorkspaceNamespaceId, ProjectTaskPlanProcessContract.Content) };
        }
        ProcessDecisionDataPolicyAdmission step = new(root.WorkspaceNamespaceId, root.WorkspaceHeadRevision, definition,
            assignment.StepInstanceId, workspace, null, null, selected.Policy, Capabilities, [policy.Budget], policy) {
            ExecutionContract = Contract,
            ChildOnBasisChange = policy.OnBasisChange == OnBasisChange.Fail ? OnBasisChange.Fail : null,
            MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1,
            ContentContract = ProjectTaskPlanProcessContract.Content,
            Funding = root.Funding,
            SharedRunScopeId = root.MaterialScope?.ScopeId
        };
        return new(root, new Dictionary<ProcessStepInstanceId, ProcessDecisionDataPolicyAdmission> { [assignment.StepInstanceId] = step });
    }

    internal static DecisionDataPolicy ResolvePolicy(DecisionDataPolicy workspace, DecisionDataPolicyOverride? preferences) {
        try {
            var policy = DecisionDataPolicyResolver.Resolve(workspace, Capabilities, authorizedInvocation: preferences);
            if (!SupportedContract.Supports(policy)) {
                throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedPolicy);
            }
            return policy;
        } catch (DecisionDataPolicyException) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedPolicy);
        }
    }

    internal static ProcessRuntimeStepAssignment RequireSingleActivity(ProcessRuntimeCommitRequest initial) {
        if (initial.InitialPlan is not { } plan || initial.InitialAssignments is not { Count: 1 } assignments ||
                plan.Steps.Count(item => item.IsExecutable) != 1 || initial.Mutation.State.Steps.Count(item => item.IsExecutable) != 1 ||
                plan.Branches.Routes.Count != 0 || plan.Subprocesses.Count != 0 || plan.ArtifactPlan.Slots.Count != 0) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedPlan);
        }
        var assignment = assignments[0];
        var planned = plan.Steps.Single(item => item.IsExecutable);
        var runtime = initial.Mutation.State.Steps.Single(item => item.IsExecutable);
        if (!assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
                planned.StepInstanceId != assignment.StepInstanceId || runtime.StepInstanceId != assignment.StepInstanceId ||
                planned.StepDefinitionId != runtime.StepDefinitionId || planned.StepKey != assignment.StepKey ||
                planned.Kind != ProcessStepKind.Activity || planned.StartsSubprocess || planned.RequiredRuntimeToolNames.Count != 0 ||
                !ProcessDecisionReviewCompletion.SupportsHostCapabilities(planned.RequiredHostCapabilities) || assignment.RequiredArtifactSlotIds.Count != 0 ||
                assignment.ProducedArtifactSlotIds.Count != 0 || assignment.BranchGate is not null ||
                !ProcessDecisionReviewCompletion.SupportsRuntimeContract(runtime)) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedPlan);
        }
        return assignment;
    }
}
