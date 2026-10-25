using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public enum ProjectMutationCallerKind { AuthenticatedSubject = 1, OpenApi = 2, LocalWorkspace = 3, GovernedExecution = 4 }
public enum ProjectDefinitionEffectKind { None = 0, Agent = 1, Process = 2 }

public sealed record ProjectCallerKey {
    public ProjectCallerKey(ReadOnlySpan<byte> bytes) {
        if (bytes.Length != 32) {
            throw new ArgumentException("A caller key requires exactly 32 bytes.", nameof(bytes));
        }
        Hex = Convert.ToHexStringLower(bytes);
    }

    public string Hex { get; }
    public byte[] ToArray() => Convert.FromHexString(Hex);
}

public sealed record ProjectMutationCallerScope {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private ProjectMutationCallerScope(ProjectMutationCallerKind kind, ProjectCallerKey key) {
        Kind = kind;
        Key = key;
    }

    public ProjectMutationCallerKind Kind { get; }
    public ProjectCallerKey Key { get; }

    public static ProjectMutationCallerScope OpenApi { get; } = Fixed(ProjectMutationCallerKind.OpenApi, "projects:open-api:v1");
    public static ProjectMutationCallerScope LocalWorkspace { get; } = Fixed(ProjectMutationCallerKind.LocalWorkspace, "projects:local-workspace:v1");

    public static ProjectMutationCallerScope FromVerifiedSubject(string issuer, string subject) {
        RequireIdentity(issuer, nameof(issuer));
        RequireIdentity(subject, nameof(subject));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "projects:verified-subject:v1");
        Append(hash, issuer);
        Append(hash, subject);
        return new(ProjectMutationCallerKind.AuthenticatedSubject, new(hash.GetHashAndReset()));
    }

    public static ProjectMutationCallerScope FromAdmittedExecution(ProjectCallerKey stableExecutionKey) {
        ArgumentNullException.ThrowIfNull(stableExecutionKey);
        if (!stableExecutionKey.ToArray().AsSpan().ContainsAnyExcept((byte)0)) {
            throw new ArgumentException("A governed caller requires a nonzero stable admitted execution key.", nameof(stableExecutionKey));
        }
        return new(ProjectMutationCallerKind.GovernedExecution, stableExecutionKey);
    }

    public ProjectCallerBinding Bind(DatasetNamespaceId @namespace) => BindKey(@namespace, Kind, Key);

    internal static ProjectCallerBinding BindKey(DatasetNamespaceId @namespace, ProjectMutationCallerKind kind, ProjectCallerKey key) {
        ArgumentNullException.ThrowIfNull(@namespace);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "projects:caller-binding:pc1");
        Append(hash, @namespace.Value.ToString("D"));
        Append(hash, ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture));
        hash.AppendData(key.ToArray());
        return new(ProjectCallerBinding.VersionOnePrefix + Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    public bool Matches(DatasetNamespaceId @namespace, ProjectCallerBinding expected)
        => expected is not null && Bind(@namespace) == expected;

    private static ProjectMutationCallerScope Fixed(ProjectMutationCallerKind kind, string domain)
        => new(kind, new(SHA256.HashData(StrictUtf8.GetBytes(domain))));

    private static void RequireIdentity(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512) {
            throw new ArgumentException("A verified issuer and subject must each contain between 1 and 512 characters.", parameterName);
        }
    }

    private static void Append(IncrementalHash hash, string value) {
        var bytes = StrictUtf8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}

public sealed record ProjectDefinitionEffectBinding {
    private static readonly byte[] NoEffect = new byte[32];
    public static ProjectDefinitionEffectBinding None { get; } = new(ProjectDefinitionEffectKind.None, 1, NoEffect);

    public ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind kind, int version, ReadOnlySpan<byte> bytes) {
        if (!Enum.IsDefined(kind) || version != 1 || bytes.Length != 32 ||
            kind == ProjectDefinitionEffectKind.None && bytes.ContainsAnyExcept((byte)0) ||
            kind != ProjectDefinitionEffectKind.None && !bytes.ContainsAnyExcept((byte)0)) {
            throw new ArgumentException("An effect binding requires a supported owner kind, version and 32-byte admitted digest.");
        }
        Kind = kind;
        Version = version;
        Hex = Convert.ToHexStringLower(bytes);
    }

    public ProjectDefinitionEffectKind Kind { get; }
    public int Version { get; }
    public string Hex { get; }
    public byte[] ToArray() => Convert.FromHexString(Hex);
}
