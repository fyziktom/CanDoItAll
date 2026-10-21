using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanMaterialCapturer(ProjectTaskPlanBasisCapture capture, ProjectProcessLaunchAuthorityService sources)
    : IProcessDecisionMaterialCapturer {
    public ProcessDecisionContentContract ContentContract => ProjectTaskPlanProcessContract.Content;

    public async Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request,
        CancellationToken cancellationToken = default) {
        request.Source.Validate();
        request.Scope.Validate();
        request.Material.Validate();
        if (request.Source.ProjectAdmission is not { } project || request.Source.DatabaseProfileId != request.Scope.DatabaseProfileId ||
                request.Material.ScopeId != request.Scope.Id || request.ContentContract != ContentContract) {
            throw new InvalidDataException("Task capture requires its original Project authority, scope and content contract.");
        }
        try {
            await using var held = await sources.AcquireResultReadAsync(request.Source, cancellationToken);
            var agentLease = held as IAgentCatalogReadLease;
            if (request.Source.Principal is ProcessLaunchPrincipal.AgentExecution && agentLease is null) {
                throw new InvalidDataException("Task capture did not acquire its original source Agent lease.");
            }
            var result = await capture.CaptureAsync(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId),
                async (_, token) => {
                    sources.RequireHeldSource(request.Source, agentLease, mutation: false);
                    await sources.RequireSourceProjectCurrentAsync(request.Source, token);
                }, cancellationToken);
            if (result is ProjectTaskPlanBasisReadResult.Captured captured) {
                var basis = captured.Basis;
                if (basis.Namespace.Value != request.Scope.WorkspaceNamespaceId) {
                    return new ProcessDecisionMaterialCaptureResult.Refused(ProcessDecisionMaterialCaptureRefusal.DatasetChanged);
                }
                return new ProcessDecisionMaterialCaptureResult.Captured(ProcessDecisionMaterial.FromContent(request.Scope,
                    request.Material, ContentContract, basis.CapturedAtUtc, Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(basis))));
            }
            if (result is not ProjectTaskPlanBasisReadResult.Refused refused) {
                throw new InvalidDataException("Task-plan capture returned an unsupported original source result.");
            }
            return new ProcessDecisionMaterialCaptureResult.Refused(refused.Reason switch {
                ProjectTaskPlanBasisRefusal.SourceMissing => ProcessDecisionMaterialCaptureRefusal.SourceMissing,
                ProjectTaskPlanBasisRefusal.LifetimeChanged => ProcessDecisionMaterialCaptureRefusal.LifetimeChanged,
                ProjectTaskPlanBasisRefusal.DatasetChanged => ProcessDecisionMaterialCaptureRefusal.DatasetChanged,
                ProjectTaskPlanBasisRefusal.InvalidSource => ProcessDecisionMaterialCaptureRefusal.InvalidSource,
                ProjectTaskPlanBasisRefusal.LimitExceeded => ProcessDecisionMaterialCaptureRefusal.LimitExceeded,
                _ => throw new InvalidDataException("Task-plan capture returned an unsupported source refusal.")
            });
        } catch (ProcessLaunchAuthorityRejectedException) {
            return new ProcessDecisionMaterialCaptureResult.Refused(ProcessDecisionMaterialCaptureRefusal.SourceChanged);
        } catch (ProjectTaskPlanBasisException failure) {
            return new ProcessDecisionMaterialCaptureResult.Refused(failure.Reason == ProjectTaskPlanBasisRefusal.LimitExceeded
                ? ProcessDecisionMaterialCaptureRefusal.LimitExceeded : ProcessDecisionMaterialCaptureRefusal.InvalidSource);
        }
    }
}
