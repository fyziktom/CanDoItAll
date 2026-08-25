using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

public sealed class ProviderSharePublication : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProviderProfileId { get; set; }

    public SharedProviderPublicationId PublicId { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
