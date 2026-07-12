using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;
using MafMemorySourceSnapshot = CanDoItAll.Memory.SourceGateway.MemorySourceSnapshot;

namespace CanDoItAll.Modules.CrmHr;

public sealed class CrmHrMemorySourceGatewayAdapter(
    ICrmHrSourceSnapshotProvider sourceSnapshotProvider) : IMemorySourceGatewayAdapter
{
    public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
        MemorySourceModuleId.Parse("crm-hr.directory"),
        MafMemorySourceKind.CrmHr,
        MemorySourceSnapshotProviderVersions.CrmHr,
        MemorySourceScope.Crm,
        RequiresPermissionCheck: true);

    public async Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceKind != Descriptor.SourceKind)
        {
            throw new InvalidOperationException(
                $"CRM/HR source adapter cannot read source kind '{request.SourceKind}'.");
        }

        if (request.RequestedScope != Descriptor.RequiredScope)
        {
            throw new InvalidOperationException(
                $"CRM/HR source adapter requires scope '{Descriptor.RequiredScope}' but received '{request.RequestedScope}'.");
        }

        return await sourceSnapshotProvider.ReadSnapshotAsync(
            new CrmHrSourceSnapshotRequest(
                request.ScopeId == Guid.Empty ? null : request.ScopeId,
                request.Cursor,
                request.Take),
            cancellationToken);
    }
}
