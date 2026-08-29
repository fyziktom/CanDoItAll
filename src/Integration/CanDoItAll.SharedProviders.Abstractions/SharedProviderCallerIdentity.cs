namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderCallerKind { Unknown, ManagedCredential, LegacyAuthenticated, AuthenticationDisabled }

public sealed record SharedProviderCallerIdentity {
    public SharedProviderCallerIdentity(SharedProviderCallerKind kind, Guid? credentialId = null,
        string? issuer = null, string? displayName = null) {
        if (!Enum.IsDefined(kind) || credentialId == Guid.Empty ||
            (kind == SharedProviderCallerKind.ManagedCredential) != credentialId.HasValue ||
            !Valid(issuer, 512) || !Valid(displayName, 256)) {
            throw new ArgumentException("The verified provider caller identity is invalid.");
        }
        Kind = kind;
        CredentialId = credentialId;
        Issuer = issuer;
        DisplayName = displayName;
    }

    public SharedProviderCallerKind Kind { get; }
    public Guid? CredentialId { get; }
    public string? Issuer { get; }
    public string? DisplayName { get; }

    private static bool Valid(string? value, int maximum) =>
        value is null || value.Length is > 0 && value.Length <= maximum && !value.Any(char.IsControl);
}
