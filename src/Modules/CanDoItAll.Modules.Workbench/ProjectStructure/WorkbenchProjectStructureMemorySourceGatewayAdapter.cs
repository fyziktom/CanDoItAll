using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;
using MafMemorySourceSnapshot = CanDoItAll.AgentFramework.Core.MemorySourceSnapshot;

namespace CanDoItAll.Modules.Workbench;

public sealed class WorkbenchProjectStructureMemorySourceGatewayAdapter(
    IProjectStructureSourceSnapshotProvider sourceSnapshotProvider) : IMemorySourceGatewayAdapter
{
    public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
        MemorySourceModuleId.Parse("workbench.project-structure"),
        MafMemorySourceKind.WorkbenchProjectStructure,
        MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure,
        MemorySourceScope.Project,
        RequiresPermissionCheck: true);

    public async Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceKind != Descriptor.SourceKind)
        {
            throw new InvalidOperationException(
                $"Workbench project source adapter cannot read source kind '{request.SourceKind}'.");
        }

        if (request.RequestedScope != Descriptor.RequiredScope)
        {
            throw new InvalidOperationException(
                $"Workbench project source adapter requires scope '{Descriptor.RequiredScope}' but received '{request.RequestedScope}'.");
        }

        return await sourceSnapshotProvider.ReadSnapshotAsync(
            new ProjectStructureSourceSnapshotRequest(request.ScopeId, request.Cursor, request.Take),
            cancellationToken);
    }
}
