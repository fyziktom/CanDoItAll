using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowFileProviderDisclosurePolicy(IDbContextFactory<WorkflowDbContext> factory,
    ICanonicalRuntimeDatabase canonical, IDatabaseRuntimeWriteFence profileFence, WorkflowRuntimeSourceObservation sources,
    IWorkspaceFileService files, IWorkspacePathResolutionService paths, IPhysicalFileSystemPathPolicyFactory physicalPolicies)
    : IWorkflowProviderDisclosurePolicy {
    public WorkflowDisclosureOwnerId Owner => WorkflowWorkspaceProviderReadEvidence.Owner;
    public bool RequiresEvidence(WorkflowNode node) => WorkflowWorkspaceProviderReadEvidence.RequiresEvidence(node);

    public async ValueTask RequireCurrentAsync(WorkflowRunSnapshot original, WorkflowDefinition definition,
        IReadOnlyList<WorkflowCompletedNodeRead> reads, CancellationToken cancellationToken = default) {
        var profile = canonical.Profile.Profile;
        await profileFence.ExecuteAsync(new DatabaseRuntimeSnapshot(profile.Id, profile.Runtime.Fingerprint, canonical.Generation),
            async token => {
                await using var database = await factory.CreateDbContextAsync(token);
                var row = await database.Set<WorkflowRunRecordEntity>().AsNoTracking().SingleOrDefaultAsync(item =>
                    item.RunId == original.RunId.Value, token) ?? throw Denied();
                var run = row.ToSnapshot();
                var version = await database.Set<WorkflowDefinitionRecord>().AsNoTracking().SingleOrDefaultAsync(item =>
                    item.WorkflowId == run.WorkflowId.Value && item.VersionId == run.VersionId.Value, token) ?? throw Denied();
                var retained = JsonSerializer.Deserialize<WorkflowDefinition>(version.DefinitionJson,
                    WorkflowProviderDisclosureContent.JsonOptions) ?? throw Denied();
                if (run.WorkflowId != definition.Id || run.VersionId != definition.VersionId ||
                        run.State is not (WorkflowRunState.Running or WorkflowRunState.Idle or WorkflowRunState.WaitingForInput) ||
                        WorkflowProviderDisclosureContent.Source(run.Origin) != WorkflowProviderDisclosureContent.Source(original.Origin) ||
                        WorkflowProviderDisclosureContent.Definition(retained) != WorkflowProviderDisclosureContent.Definition(definition)) {
                    throw Denied();
                }
                await using var heldSource = await sources.AcquireAsync(run, token);
                foreach (var read in reads) {
                    var node = definition.Graph.Nodes.Single(candidate => candidate.Id == read.Proof.NodeId);
                    var scope = node.Settings.ExecutorId == WorkflowExecutorIds.StorageFile ? files.ExecutionScope : paths.ExecutionScope;
                    if (scope.Scope != WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N"))) {
                        throw Denied();
                    }
                    if (node.Settings.ExecutorId == WorkflowExecutorIds.StorageFile && !scope.SharesIdentityWith(paths.ExecutionScope)) {
                        throw Denied();
                    }
                    await WorkflowWorkspaceProviderReadEvidence.RequireCurrentAsync(read, node, scope, physicalPolicies, token);
                }
                return true;
            }, cancellationToken);
    }

    private static InvalidOperationException Denied() => new(
        "The retained Workflow file read no longer has its original definition, live source or profile scope.");
}
