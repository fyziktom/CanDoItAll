using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;
using MafMemorySourceSnapshot = CanDoItAll.AgentFramework.Core.MemorySourceSnapshot;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowRuntimeMemorySourceGatewayAdapter(
    IWorkflowRuntimeEvidenceSourceProvider sourceSnapshotProvider) : IMemorySourceGatewayAdapter
{
    public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
        MemorySourceModuleId.Parse("agent-framework.workflow-runtime"),
        MafMemorySourceKind.WorkflowRuntime,
        MemorySourceSnapshotProviderVersions.WorkflowRuntime,
        MemorySourceScope.Workflow,
        RequiresPermissionCheck: true);

    public async Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceKind != Descriptor.SourceKind)
        {
            throw new InvalidOperationException(
                $"Workflow runtime source adapter cannot read source kind '{request.SourceKind}'.");
        }

        if (request.RequestedScope != Descriptor.RequiredScope)
        {
            throw new InvalidOperationException(
                $"Workflow runtime source adapter requires scope '{Descriptor.RequiredScope}' but received '{request.RequestedScope}'.");
        }

        return await sourceSnapshotProvider.ReadSnapshotAsync(
            new WorkflowRuntimeEvidenceSourceRequest(
                request.ScopeId == Guid.Empty ? null : new WorkflowRunId(request.ScopeId),
                request.Cursor,
                request.Take),
            cancellationToken);
    }
}
