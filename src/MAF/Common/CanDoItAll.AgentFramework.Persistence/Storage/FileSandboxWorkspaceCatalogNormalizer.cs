using System.Reflection.Metadata;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceCatalogNormalizer(
    Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog, bool> payloadRequiresSave,
    Func<SandboxWorkspaceCatalog, bool> canRetain,
    Func<SandboxWorkspaceCatalog, CatalogReadSnapshot>? normalize = null) {
    internal const long MaximumEstimatedMemoBytes = 8 * 1024 * 1024;
    private readonly Func<SandboxWorkspaceCatalog, CatalogReadSnapshot> normalize = normalize ?? Normalize;
    private CatalogReadSnapshot? memo;

    internal bool TryNormalizeUnchanged(SandboxWorkspaceCatalog source, out SandboxWorkspaceCatalog normalized) {
        var previous = Volatile.Read(ref memo);
        if (!MetadataUpdater.IsSupported && previous is not null && ReferenceEquals(previous.Source, source) &&
            canRetain(source) && previous.InputsCurrent()) {
            normalized = previous.Normalized;
            return true;
        }
        Volatile.Write(ref memo, null);
        var current = normalize(source);
        normalized = current.Normalized;
        if (!source.CatalogDataRevision.IsAssigned || RequiresSave(source, normalized)) {
            return false;
        }
        if (!MetadataUpdater.IsSupported && canRetain(source) && current.InputsCurrent()) {
            normalized = FileSandboxWorkspacePreparedValues.Freeze(normalized);
            Volatile.Write(ref memo, current with { Normalized = normalized });
        }
        return true;
    }

    internal bool RequiresSave(SandboxWorkspaceCatalog current, SandboxWorkspaceCatalog candidate)
        => payloadRequiresSave(current with { CatalogDataRevision = CatalogDataRevision.Unassigned },
            candidate with { CatalogDataRevision = CatalogDataRevision.Unassigned });

    private static CatalogReadSnapshot Normalize(SandboxWorkspaceCatalog source) {
        var inputs = new WorkspaceSeedInputReader();
        var seed = SandboxWorkspaceSeedBuilder.Build(inputs.Roots.Capabilities, inputs.Roots.Agents, inputs.ReadText);
        return new(source, SandboxWorkspaceSeedNormalizer.NormalizeCatalog(source, seed), inputs.IsCurrent);
    }
}

internal sealed record CatalogReadSnapshot(SandboxWorkspaceCatalog Source, SandboxWorkspaceCatalog Normalized, Func<bool> InputsCurrent);
