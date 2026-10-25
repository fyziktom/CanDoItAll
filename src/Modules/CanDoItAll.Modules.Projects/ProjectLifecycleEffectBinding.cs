namespace CanDoItAll.Modules.Projects;

public enum ProjectLifecycleEffectKind { None = 0, Agent = 1, Process = 2 }

public sealed record ProjectLifecycleEffectBinding {
    public static ProjectLifecycleEffectBinding None { get; } = new(ProjectLifecycleEffectKind.None, 1, new byte[32]);

    public ProjectLifecycleEffectBinding(ProjectLifecycleEffectKind kind, int version, ReadOnlySpan<byte> digest) {
        if (!Enum.IsDefined(kind) || version != 1 || digest.Length != 32 ||
                (kind == ProjectLifecycleEffectKind.None) == digest.ContainsAnyExcept((byte)0)) {
            throw new ArgumentException("A lifecycle effect requires its supported kind/version and exact admitted digest.");
        }
        Kind = kind;
        Version = version;
        Hex = Convert.ToHexStringLower(digest);
    }

    public ProjectLifecycleEffectKind Kind { get; }
    public int Version { get; }
    public string Hex { get; }
}

internal sealed record ProjectLifecycleDigest {
    internal ProjectLifecycleDigest(ReadOnlySpan<byte> bytes) {
        if (bytes.Length != 32) {
            throw new ArgumentException("A lifecycle semantic digest requires exactly 32 bytes.", nameof(bytes));
        }
        Hex = Convert.ToHexStringLower(bytes);
    }

    internal string Hex { get; }
    internal byte[] ToArray() => Convert.FromHexString(Hex);
}
