namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsKnownFileActivator(
    StorageFileToolsKnownFileResolver knownFileResolver,
    IFileAccessContextProvider contextProvider,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator) : IFileToolsKnownFileActivator
{
    public async ValueTask<FileToolsKnownFileActivation> ActivateAsync(
        FileToolsSemanticScope scope,
        FileToolsKnownFileOccurrence occurrence,
        FileToolsKnownFileIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(occurrence);
        if (!Enum.IsDefined(intent))
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }

        ResolvedStorageKnownFile resolved = await knownFileResolver.ResolveAsync(
            occurrence,
            cancellationToken);
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        FileAccessOperation operations = intent == FileToolsKnownFileIntent.Edit
            ? FileAccessOperation.View | FileAccessOperation.Edit
            : FileAccessOperation.View;
        var grantRequest = new FileAccessGrantRequest(
            context,
            scope,
            resolved.Storage.Id,
            resolved.GrantOccurrenceId,
            operations);
        var file = await authorizationCoordinator.GrantAsync(
            grantRequest,
            resolved.Reference,
            cancellationToken);
        return new FileToolsKnownFileActivation(
            new FileToolsKnownFileRequest(scope, file, intent),
            occurrence.FileName,
            occurrence.MediaType,
            occurrence.Size);
    }
}
