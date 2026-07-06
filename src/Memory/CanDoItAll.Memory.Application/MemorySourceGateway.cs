using CanDoItAll.AgentFramework.Core;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;

namespace CanDoItAll.Memory.Application;

public sealed class MemorySourceGateway : IMemorySourceGateway
{
    private readonly IReadOnlyList<IMemorySourceGatewayAdapter> adapters;
    private readonly IReadOnlySet<MafMemorySourceKind> supportedSourceKinds;

    public MemorySourceGateway(
        IReadOnlyList<IMemorySourceGatewayAdapter> adapters,
        IReadOnlyList<MafMemorySourceKind> supportedSourceKinds)
    {
        this.adapters = adapters.ToArray();
        this.supportedSourceKinds = supportedSourceKinds.ToHashSet();
    }

    public async Task<MemorySourceGatewayResult> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Policy);
        cancellationToken.ThrowIfCancellationRequested();

        if (!supportedSourceKinds.Contains(request.SourceKind))
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.UnsupportedSourceKind,
                $"Memory source kind '{request.SourceKind}' is not registered as supported by this gateway.");
        }

        if (!request.Policy.Allows(request.SourceKind))
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.DeniedSourceScope,
                $"Memory source kind '{request.SourceKind}' is denied by source gateway policy.");
        }

        if (!request.Policy.AllowsScope(request.RequestedScope))
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.DeniedSourceScope,
                $"Memory source scope '{request.RequestedScope}' is denied by source gateway policy for source kind '{request.SourceKind}'.");
        }

        var adapter = adapters.FirstOrDefault(candidate => candidate.Descriptor.SourceKind == request.SourceKind);
        if (adapter is null)
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.MissingAdapter,
                $"No memory source gateway adapter is registered for source kind '{request.SourceKind}'.");
        }

        if (adapter.Descriptor.RequiredScope != request.RequestedScope)
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.DeniedSourceScope,
                $"Memory source gateway adapter '{adapter.Descriptor.ModuleId}' requires scope '{adapter.Descriptor.RequiredScope}' but request used '{request.RequestedScope}'.");
        }

        var snapshot = await adapter.ReadSnapshotAsync(request, cancellationToken);
        var validationDiagnostic = ValidateSnapshot(adapter.Descriptor, request, snapshot);
        if (!string.IsNullOrWhiteSpace(validationDiagnostic))
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.InvalidSnapshot,
                validationDiagnostic);
        }

        if (snapshot.Manifest.SourceKind != request.SourceKind)
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.UnsupportedSourceKind,
                $"Memory source gateway adapter '{adapter.Descriptor.ModuleId}' returned source kind '{snapshot.Manifest.SourceKind}' for requested kind '{request.SourceKind}'.");
        }

        if (request.Policy.RequireRedactionForSensitivePayload &&
            snapshot.Items.Any(item => item.Permission.ContainsSensitivePayload && item.Permission.AccessMode != MemorySourceAccessMode.Redacted))
        {
            return MemorySourceGatewayResult.Rejected(
                MemorySourceGatewayStatus.RedactionRequired,
                $"Memory source gateway adapter '{adapter.Descriptor.ModuleId}' returned sensitive payload without redacted access mode.");
        }

        return MemorySourceGatewayResult.Succeeded(
            snapshot,
            MemorySourcePayloadClassifier.Classify(snapshot),
            adapter.Descriptor.ModuleId);
    }

    private static string? ValidateSnapshot(
        MemorySourceGatewayAdapterDescriptor descriptor,
        MemorySourceGatewayRequest request,
        MemorySourceSnapshot snapshot)
    {
        if (snapshot.Manifest.ScopeId != request.ScopeId)
        {
            return $"Memory source gateway adapter '{descriptor.ModuleId}' returned scope '{snapshot.Manifest.ScopeId:D}' for requested scope '{request.ScopeId:D}'.";
        }

        if (!string.Equals(snapshot.Manifest.ProviderVersion, descriptor.ProviderVersion, StringComparison.Ordinal))
        {
            return $"Memory source gateway adapter '{descriptor.ModuleId}' returned provider version '{snapshot.Manifest.ProviderVersion}' but descriptor declares '{descriptor.ProviderVersion}'.";
        }

        if (snapshot.Manifest.TotalItemCount < snapshot.Items.Count)
        {
            return $"Memory source gateway adapter '{descriptor.ModuleId}' returned total item count '{snapshot.Manifest.TotalItemCount}' below page item count '{snapshot.Items.Count}'.";
        }

        foreach (var item in snapshot.Items)
        {
            if (item.SourceKind != request.SourceKind)
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' with source kind '{item.SourceKind}' for requested kind '{request.SourceKind}'.";
            }

            if (item.Provenance.SourceKind != request.SourceKind)
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' with provenance source kind '{item.Provenance.SourceKind}' for requested kind '{request.SourceKind}'.";
            }

            if (item.Provenance.ScopeId != request.ScopeId)
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' with provenance scope '{item.Provenance.ScopeId:D}' for requested scope '{request.ScopeId:D}'.";
            }

            if (item.Provenance.EntityKind != item.EntityKind)
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' with provenance entity kind '{item.Provenance.EntityKind}' for item entity kind '{item.EntityKind}'.";
            }

            if (string.IsNullOrWhiteSpace(item.Provenance.SourceEntityId))
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' without a provenance source entity id.";
            }

            if (string.IsNullOrWhiteSpace(item.Provenance.SourceRoute))
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' without a provenance source route.";
            }

            if (string.IsNullOrWhiteSpace(item.Permission.RedactionPolicy))
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' without a redaction policy.";
            }

            if (string.IsNullOrWhiteSpace(item.Permission.AllowedFutureUsageSummary))
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' without an allowed future usage summary.";
            }

            if (string.IsNullOrWhiteSpace(item.ContentHash))
            {
                return $"Memory source gateway adapter '{descriptor.ModuleId}' returned item '{item.Id}' without a content hash.";
            }
        }

        return null;
    }
}
