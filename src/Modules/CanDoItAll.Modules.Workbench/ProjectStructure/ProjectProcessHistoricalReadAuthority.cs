using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessHistoricalReadAuthority(
    IProcessOriginalReceiptAuthorityReader reader,
    ICanonicalRuntimeDatabase database,
    IAgentExecutionProfileGenerationSource generations,
    AgentHistoricalCatalogBasis catalog,
    ProjectWriteAdmissionService projectAdmissions,
    IOptionsMonitor<ApiAccessOptions> apiOptions,
    TimeProvider clock) : IProcessHistoricalSourceAuthorityObserver {
    public async Task<ProcessSourceAuthorityObservation> ObserveHistoricalReadAsync(ProcessLaunchAuthority authority,
        ProcessHistoricalReadContext currentContext, CancellationToken cancellationToken = default) {
        authority.Validate();
        ArgumentNullException.ThrowIfNull(currentContext);
        void RequireContext() {
            if (currentContext.DatabaseProfileId != database.Profile.Profile.Id ||
                currentContext.DatabaseFingerprint != database.Profile.Profile.Runtime.Fingerprint ||
                currentContext.Generation != generations.GetGeneration().Value) {
                throw new AgentHistoricalReceiptContextChangedException();
            }
        }
        RequireContext();
        try {
            if (authority.DatabaseProfileId != currentContext.DatabaseProfileId) {
                return new(false, false);
            }
            if (authority.Principal is not ProcessLaunchPrincipal.AgentExecution source) {
                RequireOperator(authority);
                await RequireSourceProjectCurrentAsync(authority, cancellationToken);
                RequireOperator(authority);
                cancellationToken.ThrowIfCancellationRequested();
                return new(true, false);
            }
            if (authority.ProjectAdmission is not { } project || !source.Ceiling.ReadAllowed ||
                source.Ceiling.EffectiveSchemaVersion != ProcessLaunchAgentCeiling.CurrentSchemaVersion ||
                source.Ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Project &&
                    (!Guid.TryParse(source.Ceiling.WorkspaceScopeKey, out var sourceProject) || sourceProject != project.ProjectId) ||
                source.Ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Organization &&
                    (!Guid.TryParse(source.Ceiling.WorkspaceScopeKey, out var sourceProfile) || sourceProfile != project.DatabaseProfileId) ||
                source.Ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Sandbox) {
                return new(false, false);
            }
            async Task RequireAgentAsync() {
                var agent = await catalog.GetOriginalProcessSourceAsync(source.Ceiling.AgentId, cancellationToken);
                if (!ProjectDefinitionGovernedReadPolicy.CurrentAgentCanRead(source.Ceiling.AgentId, agent, ToProject(project))) {
                    throw Denied("Current Agent policy does not permit historical Process disclosure.");
                }
                ProjectProcessReadAuthorityPolicy.RequireSourceProjectGrant(authority,
                    AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson));
            }
            await RequireAgentAsync();
            await RequireSourceProjectCurrentAsync(authority, cancellationToken);
            await RequireAgentAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, false);
        } catch (Exception exception) when (exception is ProcessLaunchAuthorityRejectedException or AgentToolReceiptAccessDeniedException) {
            return new(false, false);
        } finally {
            RequireContext();
        }
    }

    internal async Task<ProjectProcessExecutionAccess?> ReadReceiptAccessAsync(ProjectProcessMutationAdmission original,
        AgentToolHistoricalReceiptReadClaim claim, CancellationToken cancellationToken) {
        var admitted = await claim.RequireAsync(cancellationToken);
        if (!ProjectDefinitionToolProposalCodec.Supports(admitted.Payload.ToolName) && !ProjectLifecycleToolProposalCodec.Supports(admitted.Payload.ToolName)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var source = ProjectDefinitionToolProposalCodec.ReadSource(admitted.Payload.SourcePreparation
            ?? throw new AgentToolReceiptAccessDeniedException());
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, admitted.OriginalSession);
        var session = admitted.OriginalSession;
        if (source.Process is not { } saved || saved.Evidence != original.Dispatch.Evidence ||
            saved.OwnerFingerprint != original.Dispatch.OwnerFingerprint || saved.ProjectReference != original.Dispatch.ProjectReference ||
            saved.Evidence.ExecutionCreatedAtUtc != admitted.Request.ExecutionCreatedAtUtc ||
            saved.Evidence.ExecutionRunId != session.Reference.ExecutionRunId || saved.Evidence.ExecutorAgentId != session.AgentId ||
            session.Reference.BackgroundSource is not { } background || background.SourceId != saved.Evidence.StepKey ||
            background.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(saved.OwnerFingerprint)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var observed = await reader.ObserveOriginalForReceiptReadAsync(saved.Evidence, cancellationToken);
        if (observed.Snapshot is not { } dispatch || dispatch.OwnerFingerprint != saved.OwnerFingerprint ||
            dispatch.Evidence != saved.Evidence || dispatch.SourceAuthority is not { } authority ||
            authority.DatabaseProfileId != session.Profile.ProfileId) {
            return null;
        }
        var current = admitted.CurrentFence.Profile;
        ProcessSourceAuthorityObservation access;
        try {
            access = await ObserveHistoricalReadAsync(authority,
                new(current.ProfileId, current.Fingerprint, current.Generation.Value), cancellationToken);
        } catch (Exception exception) when (exception is not (AgentHistoricalReceiptContextChangedException or AgentHistoricalCatalogChangedException or
            NotSupportedException or OperationCanceledException or OutOfMemoryException)) {
            throw new AgentToolReceiptReadUnavailableException(exception);
        }
        await claim.RequireAsync(cancellationToken);
        return new(dispatch, access.ReadAllowed, CanWrite: false);
    }

    internal async Task<bool> CanReadTaskPlanAsync(ProjectTaskPlanProcessCommand original,
        AgentToolHistoricalReceiptReadClaim claim, CancellationToken cancellationToken) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var source = original.Target.Source;
        if (admitted.Planning?.Decision.InheritedOrigin is not { } origin || admitted.OriginalSession != original.Target.Session ||
            source.Evidence.ExecutionCreatedAtUtc != admitted.Request.ExecutionCreatedAtUtc ||
            source.Evidence.ExecutionRunId != admitted.Request.ExecutionRunId || source.Evidence.ExecutorAgentId != admitted.Request.AgentId ||
            original.Target.Project.DatabaseProfileId != admitted.CurrentFence.Profile.ProfileId) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var parent = ProjectTaskPlanProcessCodec.Parent(origin);
        var result = await reader.ObserveOriginalDecisionForReceiptReadAsync(source.Evidence, parent,
            ProjectTaskPlanProcessContract.Execution, ProjectTaskPlanProcessContract.Content, cancellationToken);
        if (result.Snapshot is not { ObservedCurrentDispatch: false, SourceAuthority: { } authority } observed ||
            observed.Evidence != source.Evidence || observed.OwnerFingerprint != source.OwnerFingerprint ||
            observed.ProjectReference != source.ProjectReference || observed.RootRunId != source.RootRunId ||
            authority.DatabaseProfileId != admitted.CurrentFence.Profile.ProfileId) {
            return false;
        }
        var current = admitted.CurrentFence.Profile;
        var access = await ObserveHistoricalReadAsync(authority,
            new(current.ProfileId, current.Fingerprint, current.Generation.Value), cancellationToken);
        if (!access.ReadAllowed || !ProjectDefinitionGovernedReadPolicy.ProcessCanRead(new(source),
                new(observed, true, false), original.Target.Project)) {
            return false;
        }
        var matches = await projectAdmissions.CaptureAsync(original.Target.Project.ProjectId, cancellationToken) == original.Target.Project;
        await claim.RequireAsync(cancellationToken);
        return matches;
    }

    private void RequireOperator(ProcessLaunchAuthority authority) {
        var options = apiOptions.CurrentValue;
        var surface = ProjectProcessReadAuthorityPolicy.OperatorSurface(authority);
        var fingerprint = ProjectProcessReadAuthorityPolicy.OperatorFingerprint(surface, options);
        ProjectProcessReadAuthorityPolicy.RequireOperator(authority, options, fingerprint, clock.GetUtcNow());
    }

    private async Task RequireSourceProjectCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken) {
        if (ProjectProcessReadAuthorityPolicy.ResolveSourceProject(authority) is not { } source) {
            return;
        }
        var observed = await projectAdmissions.CaptureObservationAsync(source.ProjectId, cancellationToken);
        if (observed?.Admission != ToProject(source)) {
            throw Denied("The original Process source project lifetime is no longer current.");
        }
    }

    private static ProjectWriteAdmission ToProject(ProcessProjectAdmission project)
        => new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);

    private static ProcessLaunchAuthorityRejectedException Denied(string message) => new(message);
}
