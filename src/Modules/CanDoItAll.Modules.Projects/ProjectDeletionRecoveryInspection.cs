namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDeletionPreparationRegistration {
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);
    internal ProjectDeletionPreparationRegistration(ProjectDeletionParticipantId id, int protocolVersion) {
        if (string.IsNullOrWhiteSpace(id.Value) || id.Value != id.Value.Trim() ||
            id.Value.Length > ProjectDeletionObligation.MaximumParticipantIdBytes ||
            StrictUtf8.GetByteCount(id.Value) > ProjectDeletionObligation.MaximumParticipantIdBytes || protocolVersion <= 0) {
            throw new ArgumentException("Preparation registration requires a bounded participant ID and supported version.");
        }
        Id = id;
        ProtocolVersion = protocolVersion;
    }

    internal ProjectDeletionParticipantId Id { get; }
    internal int ProtocolVersion { get; }
}

public enum ProjectDeletionRecoveryReadMode { Observe = 1, Exclusive = 2 }

public sealed record ProjectDeletionRecoveryDigest {
    public ProjectDeletionRecoveryDigest(ReadOnlySpan<byte> bytes) {
        if (bytes.Length != 32) {
            throw new ArgumentException("Deletion recovery digests require exactly 32 bytes.", nameof(bytes));
        }
        Hex = Convert.ToHexStringLower(bytes);
    }

    public ProjectDeletionRecoveryDigest(string hex) {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 64 || hex.AsSpan().ContainsAnyExcept("0123456789abcdef")) {
            throw new ArgumentException("Deletion recovery digests require exact lowercase SHA-256 text.", nameof(hex));
        }
        Hex = hex;
    }

    public string Hex { get; }
}

public sealed record ProjectDeletionRecoveryParticipantEvidence {
    public ProjectDeletionRecoveryParticipantEvidence(ProjectDeletionObligation obligation, int protocolVersion,
        bool completed, ProjectDeletionRecoveryDigest digest) {
        ArgumentNullException.ThrowIfNull(obligation);
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(protocolVersion);
        Obligation = obligation;
        ProtocolVersion = protocolVersion;
        Completed = completed;
        Digest = digest;
    }

    public ProjectDeletionObligation Obligation { get; }
    public int ProtocolVersion { get; }
    public bool Completed { get; }
    public ProjectDeletionRecoveryDigest Digest { get; }
}

public interface IProjectDeletionRecoveryParticipant {
    ProjectDeletionParticipantId Id { get; }
    int ProtocolVersion { get; }
    IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; }
    Task<ProjectDeletionRecoveryParticipantEvidence> InspectAsync(ProjectDeletionPreparationContext original,
        Guid recoveryId, ProjectDeletionRecoveryReadMode mode, CancellationToken cancellationToken = default);
}
