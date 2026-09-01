using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderCallerHistoryMapper {
    public static HistoryCaller Map(SharedProviderCallerIdentity? caller, string? subject) => new(caller?.Kind switch {
        SharedProviderCallerKind.ManagedCredential => HistoryAuthenticationKind.ManagedCredential,
        SharedProviderCallerKind.LegacyAuthenticated => HistoryAuthenticationKind.LegacyAuthenticated,
        SharedProviderCallerKind.AuthenticationDisabled => HistoryAuthenticationKind.AuthenticationDisabled,
        _ => HistoryAuthenticationKind.Unknown
    }, caller?.CredentialId is { } id ? new ManagedCredentialId(id) : null, caller?.Issuer, subject, caller?.DisplayName);
}
