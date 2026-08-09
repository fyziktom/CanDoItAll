namespace CanDoItAll.Infrastructure.Storage;

public sealed record ExternalTargetRootBinding(
    string RootId,
    string HostPlatform,
    string ProtectedRootToken);

public enum ExternalTargetAliasResolutionKind
{
    NotVersionedAlias,
    Resolved,
    Invalid,
    Unbound
}

public interface IExternalTargetPathRegistry
{
    bool TryCreateAlias(string physicalPath, out string alias);

    ExternalTargetAliasResolutionKind TryResolve(
        string alias,
        out string fullPath,
        out string validationMessage);

    string MigrateLegacyAliasForWrite(string alias);

    IReadOnlyList<ExternalTargetRootBinding> ExportBindings(IEnumerable<string> aliases);
}

public interface IExternalTargetPathRegistryFactory
{
    IExternalTargetPathRegistry Create(IEnumerable<ExternalTargetRootBinding> bindings);
}
