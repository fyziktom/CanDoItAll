using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessBinding(IProcessDecisionDispatchAuthorityQuery dispatch,
    IProcessDecisionMaterialReader materials, IAgentCatalogReadLeaseStore catalog,
    ProjectProcessLaunchAuthorityService sources, ProjectWriteAdmissionService projects, ProjectTaskPlanPublisher publisher,
    ICanonicalRuntimeDatabase database, IAgentExecutionProfileGenerationSource generations) {
    internal static bool Supports(AgentToolProtocolEnvelope parent)
        => parent.Format == ProcessDecisionChildMaterialBindingCodec.Format &&
            parent.Version == ProcessDecisionChildMaterialBindingCodec.Version &&
            ProcessDecisionChildMaterialBindingCodec.Deserialize(parent.PayloadJson).ExecutionContract == ProjectTaskPlanProcessContract.Execution;

    internal async Task<AgentInheritedPlanningBinding> BindAsync(AgentInheritedPlanningBindingContext context,
        AgentToolProtocolEnvelope originalParent, AgentToolProtocolEnvelope sourceEvidence, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(originalParent);
        ArgumentNullException.ThrowIfNull(sourceEvidence);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Supports(originalParent) || sourceEvidence.Format != ProcessDecisionInitialSourceCodec.Format ||
                sourceEvidence.Version != ProcessDecisionInitialSourceCodec.Version) {
            throw Denied("Initial task planning requires its supported original Process parent and source evidence.");
        }
        var parent = ProcessDecisionChildMaterialBindingCodec.Deserialize(originalParent.PayloadJson);
        var evidence = ProcessDecisionInitialSourceCodec.Deserialize(sourceEvidence.PayloadJson);
        RequireCandidate(context, parent, evidence);
        var session = context.Session;
        void RequireProfile() {
            if (session.Profile.ProfileId != database.Profile.Profile.Id ||
                session.Profile.Fingerprint != database.Profile.Profile.Runtime.Fingerprint ||
                session.Profile.Generation != generations.GetGeneration()) {
                throw Denied("Initial task planning belongs to a different runtime profile or generation.");
            }
        }
        RequireProfile();
        var observed = await dispatch.ReadDecisionDispatchAsync(evidence, parent, ProjectTaskPlanProcessContract.Execution,
            ProjectTaskPlanProcessContract.Content, cancellationToken);
        if (observed.Snapshot is not { ObservedCurrentDispatch: true, SourceAuthority: { CanCreateTasks: true, ProjectAdmission: { } admittedProject } authority } source ||
            observed.OriginalDecisionPolicy is not { } policy || source.Evidence != evidence ||
            session.Reference.BackgroundSource!.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(source.OwnerFingerprint)) {
            throw Denied("The original child no longer has its exact Process dispatch, policy and task authority.");
        }
        var launcher = (authority.Principal as ProcessLaunchPrincipal.AgentExecution)?.Ceiling.AgentId;
        Guid[] selected = launcher is { } id && id != session.AgentId ? [session.AgentId, id] : [session.AgentId];
        await using var held = await catalog.AcquireAgentAuthoritySnapshotAsync(selected, cancellationToken);
        void RequireHeld() {
            RequireProfile();
            var executor = ProjectProcessLaunchAuthorityService.FindHeldAgent(held, session.AgentId);
            if (held.Scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")) ||
                executor is null || executor.IsTemplate || executor.Status != AgentLifecycleStatus.Active ||
                !executor.Permissions.CanUseTools ||
                !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(AgentProjectStructureAccessMetadata.Read(executor.ConfigurationJson))) {
                throw Denied("The selected executor no longer permits task planning.");
            }
            sources.RequireHeldSource(authority, held, mutation: true);
        }
        RequireHeld();
        ProjectWriteAdmission project = new(admittedProject.DatabaseProfileId, admittedProject.ProjectId, admittedProject.LifetimeId);
        await projects.RequireCurrentAsync(project, cancellationToken);
        if (ProjectProcessReadAuthorityPolicy.ResolveSourceProject(authority) is { } admittedSource &&
            new ProjectWriteAdmission(admittedSource.DatabaseProfileId, admittedSource.ProjectId, admittedSource.LifetimeId) is var sourceProject &&
            sourceProject != project) {
            await projects.RequireCurrentAsync(sourceProject, cancellationToken);
        }
        var material = await materials.ReadAsync(parent.Scope, parent.Material.Reference, cancellationToken)
            ?? throw Denied("The original task-plan material is unavailable.");
        var caller = new ProjectTaskPlanCaller(ProjectTaskPlanCallerKind.ProcessAgent,
            ProjectDefinitionGovernedBindings.ProcessCaller(source).Key.ToArray());
        var writeContext = await publisher.GetWriteContextAsync(caller, cancellationToken);
        var value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1, material.PayloadJson);
        AgentPlanningInheritedOrigin origin = new(session.Profile, parent.Scope.WorkspaceNamespaceId, session.AgentId,
            session.Reference.BackgroundSource!, originalParent, parent.Child.MaterialId.Value, material.CapturedAtUtc, value.Digest,
            policy, new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant)) {
                ChildOnBasisChange = observed.OriginalChildOnBasisChange
            };
        ProjectTaskPlanProcessTarget target = new(session, project, source, new(true, true), writeContext);
        ProjectTaskPlanProcessCodec.RequireTarget(target, origin);
        var captured = ProjectTaskPlanProcessCodec.ReadBasis(origin, material);
        if (captured.Material.Project.Expectation.Project != project) {
            throw Denied("The original task material belongs to a different Project lifetime.");
        }
        RequireHeld();
        cancellationToken.ThrowIfCancellationRequested();
        return new(ProjectTaskPlanProcessCodec.WriteTarget(target), origin);
    }

    private static void RequireCandidate(AgentInheritedPlanningBindingContext context,
        ProcessDecisionChildMaterialBinding parent, ProcessExecutionClaimEvidence evidence) {
        var candidate = context.Candidate;
        var session = context.Session;
        if (candidate.Id != parent.Child.Address.ChildId.Value || candidate.Id != evidence.ExecutionRunId ||
            candidate.AgentId != evidence.ExecutorAgentId || session.AgentId != candidate.AgentId ||
            session.Reference.ExecutionRunId != candidate.Id || session.Reference.ChatSessionId != Guid.Empty ||
            session.Reference.AuthorityId.Value != Guid.Empty || session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
            session.Reference.BackgroundSource is not { SourceKind: ProjectTaskPlanProcessContract.SourceKind } source ||
            source.SourceId != evidence.StepKey || candidate.SourceKind != source.SourceKind || candidate.SourceId != source.SourceId ||
            !Guid.TryParse(candidate.ProcessRunId, out var runId) || runId != evidence.RunId.Value ||
            !Guid.TryParse(candidate.ProcessStepId, out var stepId) || stepId != evidence.StepInstanceId.Value ||
            !Guid.TryParse(candidate.CorrelationId, out var correlation) || correlation != runId ||
            !Guid.TryParse(candidate.CausationId, out var causation) || causation != stepId ||
            candidate.RequestedBy != ProjectTaskPlanProcessContract.ProcessRequester ||
            candidate.RequestedByKind != ProjectTaskPlanProcessContract.SystemRequesterKind ||
            candidate.CreatedAtUtc != evidence.ExecutionCreatedAtUtc || candidate.Revision != 1 ||
            candidate.State != ExecutionState.Preparing || candidate.Outcome is not null || candidate.ToolAdmission is not null ||
            candidate.ChatSessionId is not null || candidate.CompletedAtUtc is not null) {
            throw Denied("Initial task planning requires the exact new child candidate and its admitted background session.");
        }
    }

    private static AgentToolAdmissionException Denied(string message) => ProjectTaskPlanAgentCodec.Denied(message);
}
