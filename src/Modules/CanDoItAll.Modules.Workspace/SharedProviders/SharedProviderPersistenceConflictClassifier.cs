using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workspace;

internal static class SharedProviderPersistenceConflictClassifier
{
    private const string PublicationProviderProfileIndex =
        "IX_Workspace_ProviderSharePublications_ProviderProfileId";
    private const string ImportSourcePublicationIndex =
        "IX_Workspace_SharedProviderImports_SourceId_RemotePublicationId";
    private const string ImportProviderProfileIndex =
        "IX_Workspace_SharedProviderImports_ProviderProfileId";

    public static bool IsPublicationProviderIdentityConflict(Exception exception)
        => IsUniqueConstraintViolation(exception, PublicationProviderProfileIndex);

    public static bool IsReconciliationIdentityConflict(Exception exception)
        => IsUniqueConstraintViolation(
            exception,
            ImportSourcePublicationIndex,
            ImportProviderProfileIndex);

    private static bool IsUniqueConstraintViolation(
        Exception exception,
        params string[] constraintNames)
    {
        var names = constraintNames.ToHashSet(StringComparer.Ordinal);
        return SerializableMutationScope.IsUniqueConstraintConflict(exception, names);
    }
}
