using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Tests.Components.Memory;

internal sealed class MemoryTestDatabase : ICanonicalRuntimeDatabase {
    public ResolvedDatabaseProfile Profile { get; } = new(new DatabaseProfileRecord {
        Id = Guid.NewGuid(), ProviderKind = DatabaseProviderKind.InMemory,
        SourceKind = DatabaseProfileSourceKind.InMemory, InMemory = new InMemoryDatabaseProfileConnection()
    }, DatabaseProfileResolutionSource.ExplicitOverride, "in-memory");
    public long Generation { get; set; } = 1;
}
