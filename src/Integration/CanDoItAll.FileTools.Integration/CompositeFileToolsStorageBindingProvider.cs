using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

internal sealed class CompositeFileToolsStorageBindingProvider(
    IEnumerable<IFileToolsStorageBindingSource> sources) : IFileToolsStorageBindingProvider
{
    public ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        IFileToolsStorageBindingSource? owner = null;
        foreach (IFileToolsStorageBindingSource source in sources)
        {
            if (source.ScopeKind != scope.Kind)
            {
                continue;
            }

            if (owner is not null)
            {
                return ValueTask.FromException<IReadOnlyList<FileToolsStorageBinding>>(
                    new FileBrowserProviderException(new FileBrowserError(
                        FileBrowserErrorCode.CorruptProviderResponse,
                        "More than one module owns the same semantic file scope.")));
            }

            owner = source;
        }

        if (owner is null)
        {
            return ValueTask.FromException<IReadOnlyList<FileToolsStorageBinding>>(
                new FileBrowserProviderException(new FileBrowserError(
                    FileBrowserErrorCode.Unsupported,
                    "No module owns storage bindings for this semantic file scope.")));
        }

        return owner.ResolveAsync(scope, cancellationToken);
    }
}
