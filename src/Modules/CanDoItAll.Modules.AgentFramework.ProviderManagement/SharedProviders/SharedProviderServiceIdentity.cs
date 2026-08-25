using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderServiceIdentity
{
    public static readonly Guid SingletonId =
        Guid.Parse("7d5f45ad-9b13-4f1a-9284-260e2e07c92c");

    public Guid Id { get; set; } = SingletonId;

    public SharedProviderSourceInstanceId PublicId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static SharedProviderServiceIdentity Create(
        SharedProviderSourceInstanceId publicId,
        DateTimeOffset timestampUtc)
    {
        SharedProviderStateGuard.SourceInstanceId(publicId, nameof(publicId));
        SharedProviderStateGuard.Utc(timestampUtc, nameof(timestampUtc));
        return new SharedProviderServiceIdentity
        {
            PublicId = publicId,
            CreatedAtUtc = timestampUtc
        };
    }
}
