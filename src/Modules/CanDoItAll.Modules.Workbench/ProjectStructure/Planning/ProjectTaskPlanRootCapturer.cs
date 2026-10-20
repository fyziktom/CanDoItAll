using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanRootCapturer(ProjectTaskPlanBasisCapture capture, ProjectProcessLaunchAuthorityService sources)
    : IProcessDecisionRootCapturer {
    public ProcessDecisionContentContract ContentContract => ProjectTaskPlanProcessContract.Content;

    public async Task<ProcessDecisionRootCaptureResult> CaptureAsync(ProcessDecisionRootCaptureRequest request,
        CancellationToken cancellationToken = default) {
        request.Source.Validate();
        request.Scope.Validate();
        request.Material.Validate();
        if (request.Source.ProjectAdmission is not { } project || request.Source.DatabaseProfileId != request.Scope.DatabaseProfileId ||
                request.Material.ScopeId != request.Scope.ScopeId || request.Scope.ContentContract != ContentContract ||
                request.Material.Generation != 1) {
            throw new InvalidDataException("Root task capture requires its original Project authority, scope and initial content contract.");
        }
        try {
            await using var held = await sources.AcquireResultReadAsync(request.Source, cancellationToken);
            var agentLease = held as IAgentCatalogReadLease;
            if (request.Source.Principal is ProcessLaunchPrincipal.AgentExecution && agentLease is null) {
                throw new InvalidDataException("Root capture did not acquire its original source Agent lease.");
            }
            var result = await capture.CaptureAsync(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId),
                async (_, token) => {
                    sources.RequireHeldSource(request.Source, agentLease, mutation: false);
                    await sources.RequireSourceProjectCurrentAsync(request.Source, token);
                }, cancellationToken);
            if (result is ProjectTaskPlanBasisReadResult.Captured captured) {
                var basis = captured.Basis;
                if (basis.Namespace.Value != request.Scope.WorkspaceNamespaceId) {
                    return new ProcessDecisionRootCaptureResult.Refused(ProcessDecisionRootCaptureRefusal.DatasetChanged);
                }
                return new ProcessDecisionRootCaptureResult.Captured(ProcessDecisionMaterial.FromContent(request.Scope.MaterialScope,
                    request.Material, ContentContract, basis.CapturedAtUtc, Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(basis))));
            }
            if (result is not ProjectTaskPlanBasisReadResult.Refused refused) {
                throw new InvalidDataException("Task-plan capture returned an unsupported original source result.");
            }
            return new ProcessDecisionRootCaptureResult.Refused(refused.Reason switch {
                ProjectTaskPlanBasisRefusal.SourceMissing => ProcessDecisionRootCaptureRefusal.SourceMissing,
                ProjectTaskPlanBasisRefusal.LifetimeChanged => ProcessDecisionRootCaptureRefusal.LifetimeChanged,
                ProjectTaskPlanBasisRefusal.DatasetChanged => ProcessDecisionRootCaptureRefusal.DatasetChanged,
                ProjectTaskPlanBasisRefusal.InvalidSource => ProcessDecisionRootCaptureRefusal.InvalidSource,
                ProjectTaskPlanBasisRefusal.LimitExceeded => ProcessDecisionRootCaptureRefusal.LimitExceeded,
                _ => throw new InvalidDataException("Task-plan capture returned an unsupported source refusal.")
            });
        } catch (ProcessLaunchAuthorityRejectedException) {
            return new ProcessDecisionRootCaptureResult.Refused(ProcessDecisionRootCaptureRefusal.SourceChanged);
        } catch (ProjectTaskPlanBasisException failure) {
            return new ProcessDecisionRootCaptureResult.Refused(failure.Reason == ProjectTaskPlanBasisRefusal.LimitExceeded
                ? ProcessDecisionRootCaptureRefusal.LimitExceeded : ProcessDecisionRootCaptureRefusal.InvalidSource);
        }
    }
}
