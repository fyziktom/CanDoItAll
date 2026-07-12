using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;
using MafMemorySourceSnapshot = CanDoItAll.Memory.SourceGateway.MemorySourceSnapshot;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourceMemorySourceGatewayAdapter(
    IResourceSourceSnapshotProvider sourceSnapshotProvider) : IMemorySourceGatewayAdapter
{
    public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
        MemorySourceModuleId.Parse("resources.project-resources"),
        MafMemorySourceKind.ResourceCatalog,
        MemorySourceSnapshotProviderVersions.ResourceCatalog,
        MemorySourceScope.Resource,
        RequiresPermissionCheck: true);

    public async Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceKind != Descriptor.SourceKind)
        {
            throw new InvalidOperationException(
                $"Resource source adapter cannot read source kind '{request.SourceKind}'.");
        }

        if (request.RequestedScope != Descriptor.RequiredScope)
        {
            throw new InvalidOperationException(
                $"Resource source adapter requires scope '{Descriptor.RequiredScope}' but received '{request.RequestedScope}'.");
        }

        return await sourceSnapshotProvider.ReadSnapshotAsync(
            new ResourceSourceSnapshotRequest(
                request.ScopeId == Guid.Empty ? null : request.ScopeId,
                ProjectId: null,
                request.Cursor,
                request.Take),
            cancellationToken);
    }
}
