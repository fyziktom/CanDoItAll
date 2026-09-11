using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowRuntimeSourceObservation(ICanonicalRuntimeDatabase canonical,
    ProjectStructureWorkflowAuthorityService sources, IWorkflowProcessToolSourceAuthority processTools,
    IOptionsMonitor<ApiAccessOptions> apiOptions) {
    public async Task<IAsyncDisposable?> AcquireAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken = default) {
        if (run.Origin?.AuthorizationScope is not { } scope ||
                run.Origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint) {
            throw Denied();
        }
        if (run.Origin.StructureAuthority is not null ||
                run.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment or WorkflowLaunchOrigin.ProcessAssignment) {
            return await sources.AcquireProviderDisclosureAsync(run, cancellationToken);
        }
        if (run.Origin is WorkflowLaunchOrigin.ProcessToolInvocation tool) {
            return await processTools.AcquireDisclosureAsync(tool, cancellationToken);
        }
        if (scope != WorkspaceScopeDescriptor.Organization(canonical.Profile.Profile.Id.ToString("N")) ||
                run.Origin is not (WorkflowLaunchOrigin.Api or WorkflowLaunchOrigin.Preview) ||
                run.Origin is WorkflowLaunchOrigin.Api api && (api.Actor.Kind != WorkflowLaunchActorKind.User || !apiOptions.CurrentValue.Enabled) ||
                run.Origin is WorkflowLaunchOrigin.Preview preview && preview.Actor.Kind != WorkflowLaunchActorKind.User) {
            throw Denied();
        }
        return null;
    }

    private static WorkflowRuntimeSourceRejectedException Denied() => new(
        "The retained Workflow response no longer has its original live source and profile authority.");
}

internal sealed class WorkflowRuntimeSourceRejectedException(string message) : InvalidOperationException(message);
