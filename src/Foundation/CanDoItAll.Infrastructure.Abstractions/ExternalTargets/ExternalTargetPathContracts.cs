namespace CanDoItAll.Infrastructure.Storage;

/// <summary>
/// Server-issued binding of an external-target root identifier to a folder on a host, used by agent workspace access to
/// resolve external-target aliases. The folder path is not readable in the binding: it is encrypted in the protected
/// token. Treat bindings as sensitive and send them back exactly as read; clients never create them.
/// </summary>
/// <param name="RootId">
/// Root identifier used inside external-target aliases: 24 lower-case hexadecimal characters.
/// </param>
/// <param name="HostPlatform">
/// Platform of the host the folder belongs to: <c>windows</c>, <c>macos</c>, <c>linux</c> or <c>other</c>. A binding
/// only resolves on a host of the same platform.
/// </param>
/// <param name="ProtectedRootToken">
/// Encrypted token holding the folder location, readable only by the issuing host's data protection keys. Opaque; do
/// not change it.
/// </param>
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
