using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanAgentParticipant(IAgentCatalogReadLeaseStore catalog,
    ProjectAgentSourceMutationAuthority source, ProjectWriteAdmissionService projects,
    ProjectTaskPlanBasisCapture capture, ProjectTaskPlanPublisher publisher, ProjectTaskPlanAgentAdmission admission) : IAgentPlanningParticipant {
    internal static DecisionDataPolicyCapabilities Capabilities { get; } = new(true, true, false, true, true, true,
        PublicationFreshness.RequireCurrent, new(4, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));
    public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
    public AgentPlanningPublicationAssessment ClassifyPublication(AgentToolSessionObservation original,
        AgentToolPreparedPayload payload, AgentSavedEffectDispatchResult result)
        => ProjectTaskPlanAgentResult.Classify(original, payload, result);
    public Task<AgentPlanningRunAdmission> PrepareRunAsync(AgentPlanningRunPreparation preparation, CancellationToken cancellationToken)
        => admission.PrepareAsync(preparation, cancellationToken);
    public AgentPlanningPromptContract Prompt => ProjectTaskPlanPlanningContent.Prompt;

    internal async Task<AgentToolProtocolEnvelope> CaptureTargetAsync(AgentToolSessionAdmission session,
        AgentExecutionGovernanceSnapshot governance, ProjectWriteAdmission project, CancellationToken cancellationToken) {
        await using var held = await catalog.AcquireAgentReadLeaseAsync(session.AgentId, cancellationToken);
        var agent = held.Agent ?? throw ProjectTaskPlanAgentCodec.Denied("The original Agent is unavailable.");
        var target = new ProjectTaskPlanAgentTarget(session, project, new(AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson),
            governance, governance.SourceProjectLifetime is { } origin
                ? new(origin.DatabaseProfileId, origin.ProjectId, origin.LifetimeId) : null));
        source.RequireSource(target.Admission, MutationTarget(target), held);
        foreach (var expected in Projects(target)) {
            await projects.RequireCurrentAsync(expected, cancellationToken);
        }
        return ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.TargetFormat, target,
            AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
    }

    public async Task<AgentToolProtocolEnvelope> CaptureBasisAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
        var target = ProjectTaskPlanAgentCodec.Target(decision);
        await using var held = await catalog.AcquireAgentReadLeaseAsync(target.Session.AgentId, cancellationToken);
        RequireHeldRead(target, held);
        foreach (var expected in Projects(target)) {
            await projects.RequireCurrentAsync(expected, cancellationToken);
        }
        var current = await capture.CaptureAsync(target.Project, (_, token) => {
            token.ThrowIfCancellationRequested();
            RequireHeldRead(target, held);
            return Task.CompletedTask;
        }, cancellationToken);
        if (current is not ProjectTaskPlanBasisReadResult.Captured captured) {
            throw ProjectTaskPlanAgentCodec.Denied("The original target's complete task basis could not be captured.");
        }
        var context = await publisher.GetWriteContextAsync(target.Caller, cancellationToken);
        if (context.Namespace != captured.Basis.Namespace || context.Namespace.Value != decision.Provenance?.WorkspaceNamespaceId) {
            throw ProjectTaskPlanAgentCodec.Denied("The dataset changed while capturing the task-plan basis.");
        }
        using var material = JsonDocument.Parse(ProjectTaskPlanBasisCodec.Encode(captured.Basis));
        return ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.BasisFormat,
            new ProjectTaskPlanAgentBasis(material.RootElement.Clone(), context), AgentPlanningProviderBounds.MaximumInputUtf8Bytes);
    }

    public async Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
        var target = ProjectTaskPlanAgentCodec.Target(decision);
        await using var held = await catalog.AcquireAgentReadLeaseAsync(target.Session.AgentId, cancellationToken);
        RequireHeldRead(target, held);
        foreach (var expected in Projects(target)) {
            await projects.RequireCurrentAsync(expected, cancellationToken);
        }
    }

    public string DescribePublication(AgentToolSessionObservation original, AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) {
        var planning = original.Planning ?? throw ProjectTaskPlanAgentCodec.Denied("The original planning observation is missing.");
        var command = ProjectTaskPlanAgentCodec.Resolve(planning, original.Session, payload, binding);
        return ProjectTaskPlanPlanningContent.Describe(command.Command);
    }

    internal async ValueTask<bool> CanReadAsync(AgentToolSessionObservation observation, CancellationToken cancellationToken) {
        if (observation.Planning is not { } planning || planning.Decision.Session != observation.Session.Reference) {
            return false;
        }
        try {
            await RequireCurrentReadAsync(planning.Decision, cancellationToken);
            return true;
        } catch (UnauthorizedAccessException) {
            return false;
        } catch (ProjectWriteAdmissionRejectedException) {
            return false;
        }
    }

    public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision,
        AgentPlanningBasisRecord basis, string responseText)
        => ProjectTaskPlanPlanningContent.Prepare(() => ProjectTaskPlanAgentCodec.Prepare(decision, basis, responseText));

    internal static ProjectMutationTarget MutationTarget(ProjectTaskPlanAgentTarget target)
        => new(ProjectMutationPurpose.ExistingWrite, target.Project.ProjectId, [target.Project]);

    internal static IReadOnlyList<ProjectWriteAdmission> Projects(ProjectTaskPlanAgentTarget target)
        => target.Agent.SourceProject is { } origin && origin != target.Project ? [target.Project, origin] : [target.Project];

    internal static void RequireHeldRead(ProjectTaskPlanAgentTarget target, IAgentCatalogReadLease held) {
        if (held.Scope != WorkspaceScopeDescriptor.Organization(target.Session.Profile.ProfileId.ToString("N")) ||
            Projects(target).Any(project => !ProjectDefinitionGovernedReadPolicy.AgentCanRead(target.Admission, held.Agent, project))) {
            throw new UnauthorizedAccessException("Current authorization does not permit reading the original task-plan target.");
        }
    }
}
