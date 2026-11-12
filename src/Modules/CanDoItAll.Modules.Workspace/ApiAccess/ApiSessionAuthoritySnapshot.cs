using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed record ApiSessionAuthoritySnapshot {
    public ApiSessionAuthoritySnapshot(ApiSessionIdentity identity, string normalizedUserName, ApiUserAuthorityGeneration? generation) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(identity.Scopes);
        if (identity.IsAdministrator != (generation is null) ||
                normalizedUserName != ApiIdentityRules.NormalizeUserName(normalizedUserName)) {
            throw new ArgumentException("A selected session requires its original normalized identity and ordinary-account generation.");
        }
        Identity = identity with { Scopes = Array.AsReadOnly(identity.Scopes.ToArray()) };
        NormalizedUserName = normalizedUserName;
        Generation = generation;
    }

    public ApiSessionIdentity Identity { get; }
    public string NormalizedUserName { get; }
    public ApiUserAuthorityGeneration? Generation { get; }
}
