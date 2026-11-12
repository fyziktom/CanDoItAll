using System.Collections.Immutable;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatabaseTransferTargetPlan {
    public DatabaseTransferTargetPlan(IEnumerable<Type> entityTypes, IEnumerable<long>? sessionAdvisoryKeys = null) {
        ArgumentNullException.ThrowIfNull(entityTypes);
        EntityTypes = entityTypes.Distinct().ToImmutableArray();
        if (EntityTypes.IsEmpty || EntityTypes.Any(type => type is null)) {
            throw new ArgumentException("A transfer requires a nonempty complete target entity plan.", nameof(entityTypes));
        }
        SessionAdvisoryKeys = (sessionAdvisoryKeys ?? []).Distinct().Order().ToImmutableArray();
    }

    public ImmutableArray<Type> EntityTypes { get; }
    public ImmutableArray<long> SessionAdvisoryKeys { get; }
}
