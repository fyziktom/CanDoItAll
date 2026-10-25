using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessReadAuthority(IProcessOriginalReceiptAuthorityReader originals,
    ICanonicalRuntimeDatabase database, IAgentExecutionProfileGenerationSource generations,
    IAgentCatalogReadLeaseStore catalog, ProjectProcessLaunchAuthorityService source, ProjectWriteAdmissionService projects) {
    internal async Task RequireAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
        var target = ProjectTaskPlanProcessCodec.Target(decision);
        var origin = decision.InheritedOrigin!;
        void RequireProfile() {
            if (target.Session.Profile.ProfileId != database.Profile.Profile.Id ||
                target.Session.Profile.Fingerprint != database.Profile.Profile.Runtime.Fingerprint ||
                target.Session.Profile.Generation != generations.GetGeneration()) {
                throw new AgentToolReceiptAccessDeniedException();
            }
        }
        RequireProfile();
        try {
            var result = await originals.ObserveOriginalDecisionForReceiptReadAsync(target.Source.Evidence,
                ProjectTaskPlanProcessCodec.Parent(origin), ProjectTaskPlanProcessContract.Execution,
                ProjectTaskPlanProcessContract.Content, cancellationToken);
            if (result.OriginalDecisionPolicy != origin.ParentPolicy || result.OriginalChildOnBasisChange != origin.ChildOnBasisChange ||
                result.Snapshot is not { ObservedCurrentDispatch: false, SourceAuthority: { } authority } current ||
                current.Evidence != target.Source.Evidence || current.OwnerFingerprint != target.Source.OwnerFingerprint ||
                current.RootRunId != target.Source.RootRunId || current.ProjectReference != target.Source.ProjectReference ||
                !ProjectDefinitionGovernedReadPolicy.ProcessCanRead(new(target.Source), new(current, true, false), target.Project)) {
                throw new AgentToolReceiptAccessDeniedException();
            }
            var launcher = (authority.Principal as ProcessLaunchPrincipal.AgentExecution)?.Ceiling.AgentId;
            Guid[] selected = launcher is { } id && id != target.Session.AgentId ? [target.Session.AgentId, id] : [target.Session.AgentId];
            await using var held = await catalog.AcquireAgentAuthoritySnapshotAsync(selected, cancellationToken);
            void RequireHeld() {
                var executor = ProjectProcessLaunchAuthorityService.FindHeldAgent(held, target.Session.AgentId);
                if (held.Scope != WorkspaceScopeDescriptor.Organization(target.Session.Profile.ProfileId.ToString("N")) ||
                    executor is null || executor.IsTemplate || executor.Status != AgentLifecycleStatus.Active || !executor.Permissions.CanUseTools) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                source.RequireHeldSource(authority, held, mutation: false);
            }
            RequireHeld();
            await projects.RequireCurrentAsync(target.Project, cancellationToken);
            if (ProjectProcessReadAuthorityPolicy.ResolveSourceProject(authority) is { } project &&
                new ProjectWriteAdmission(project.DatabaseProfileId, project.ProjectId, project.LifetimeId) is var original && original != target.Project) {
                await projects.RequireCurrentAsync(original, cancellationToken);
            }
            RequireHeld();
            cancellationToken.ThrowIfCancellationRequested();
        } catch (ProcessLaunchAuthorityRejectedException) {
            throw new AgentToolReceiptAccessDeniedException();
        } catch (ProjectWriteAdmissionRejectedException) {
            throw new AgentToolReceiptAccessDeniedException();
        } finally {
            RequireProfile();
        }
    }
}
