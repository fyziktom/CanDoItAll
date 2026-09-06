using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

[Flags]
public enum SharedProviderProfileReferenceKinds
{
    None = 0,
    Publication = 1,
    Import = 2
}

public sealed class SharedProviderProfileDeletionBlockedException : InvalidOperationException
{
    internal SharedProviderProfileDeletionBlockedException(
        Guid providerProfileId,
        SharedProviderProfileReferenceKinds referenceKinds)
        : base(CreateMessage(providerProfileId, referenceKinds))
    {
        ProviderProfileId = providerProfileId;
        ReferenceKinds = referenceKinds;
    }

    public Guid ProviderProfileId { get; }

    public SharedProviderProfileReferenceKinds ReferenceKinds { get; }

    private static string CreateMessage(
        Guid providerProfileId,
        SharedProviderProfileReferenceKinds referenceKinds)
    {
        var referenceDescription = referenceKinds switch
        {
            SharedProviderProfileReferenceKinds.Publication => "a shared-provider publication",
            SharedProviderProfileReferenceKinds.Import => "a shared-provider import",
            SharedProviderProfileReferenceKinds.Publication |
                SharedProviderProfileReferenceKinds.Import =>
                    "a shared-provider publication and import",
            _ => throw new ArgumentOutOfRangeException(
                nameof(referenceKinds),
                referenceKinds,
                "At least one known shared-provider reference is required.")
        };

        return $"Provider profile '{providerProfileId:D}' cannot be deleted because it is referenced by {referenceDescription}. {SharedProviderDeletionMessages.For(referenceKinds)}";
    }
}

public sealed class SharedProviderProfileDeletionGuard :
    IProviderProfileDeletionGuard
{
    public async Task EnsureCanDeleteAsync(
        AppDbContext dbContext,
        Guid providerProfileId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "The provider profile id cannot be empty.",
                nameof(providerProfileId));
        }

        var referenceKinds = SharedProviderProfileReferenceKinds.None;
        if (await dbContext.Set<ProviderSharePublication>()
            .AsNoTracking()
            .AnyAsync(
                publication => publication.ProviderProfileId == providerProfileId,
                cancellationToken))
        {
            referenceKinds |= SharedProviderProfileReferenceKinds.Publication;
        }

        if (await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .AnyAsync(
                import => import.ProviderProfileId == providerProfileId,
                cancellationToken))
        {
            referenceKinds |= SharedProviderProfileReferenceKinds.Import;
        }

        if (referenceKinds != SharedProviderProfileReferenceKinds.None)
        {
            throw new SharedProviderProfileDeletionBlockedException(
                providerProfileId,
                referenceKinds);
        }
    }
}


public static class SharedProviderDeletionMessages {
    public static string For(SharedProviderProfileReferenceKinds kinds) => kinds switch {
        SharedProviderProfileReferenceKinds.Publication =>
            "The permanent publication identity prevents deletion. Unpublish only stops sharing.",
        SharedProviderProfileReferenceKinds.Import =>
            "Retire the imported provider instead of deleting it. Its audit identity remains.",
        SharedProviderProfileReferenceKinds.Publication | SharedProviderProfileReferenceKinds.Import =>
            "The permanent publication identity prevents deletion; Unpublish only stops sharing. Retire the import; its audit identity remains.",
        _ => "Permanent provider references prevent deletion."
    };

    public const string SourceWithImports =
        "Retire or migrate the source's imports before deleting the source. Retained audit references continue to prevent deletion.";
}
