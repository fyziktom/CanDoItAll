using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectDeletionCompletionId {
    public ProjectDeletionCompletionId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("Deletion completion requires a nonempty identity.", nameof(value));
        }
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ProjectDeletionCompletionAddress {
    public ProjectDeletionCompletionAddress(Guid databaseProfileId, DatasetNamespaceId @namespace,
        ProjectDeletionCompletionId id) {
        if (databaseProfileId == Guid.Empty) {
            throw new ArgumentException("Deletion completion requires its original profile.", nameof(databaseProfileId));
        }
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(id);
        DatabaseProfileId = databaseProfileId;
        Namespace = @namespace;
        Id = id;
    }

    public Guid DatabaseProfileId { get; }
    public DatasetNamespaceId Namespace { get; }
    public ProjectDeletionCompletionId Id { get; }
}

public sealed record ProjectDeletionPreparationContext {
    public ProjectDeletionPreparationContext(ProjectDeletionCompletionAddress completion, ProjectWriteAdmission project) {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(project);
        if (completion.DatabaseProfileId != project.DatabaseProfileId) {
            throw new ArgumentException("Deletion preparation cannot change the original completion profile.");
        }
        Completion = completion;
        Project = project;
    }

    public ProjectDeletionCompletionAddress Completion { get; }
    public ProjectWriteAdmission Project { get; }
}

public sealed record ProjectDeletionObligation {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public const int MaximumParticipantIdBytes = 64;

    public ProjectDeletionObligation(ProjectDeletionParticipantId participant, Guid recoveryId) {
        if (participant.Value is null || participant.Value.Length > MaximumParticipantIdBytes ||
                string.IsNullOrWhiteSpace(participant.Value) ||
                !string.Equals(participant.Value, participant.Value.Trim(), StringComparison.Ordinal) ||
                StrictUtf8.GetByteCount(participant.Value) > MaximumParticipantIdBytes || recoveryId == Guid.Empty) {
            throw new ArgumentException("A deletion obligation requires a bounded participant and nonempty recovery identity.");
        }
        Participant = participant;
        RecoveryId = recoveryId;
    }

    public ProjectDeletionParticipantId Participant { get; }
    public Guid RecoveryId { get; }
}

public sealed record ProjectDeletionCompletionBinding {
    public const int MaximumParticipants = 16;

    public ProjectDeletionCompletionBinding(ProjectDeletionPreparationContext original,
        IReadOnlyList<ProjectDeletionObligation> obligations) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(obligations);
        var count = obligations.Count;
        if (count < 0 || count > MaximumParticipants) {
            throw new ArgumentException("Deletion completion exceeds the participant bound.", nameof(obligations));
        }
        var captured = new ProjectDeletionObligation[count];
        var participants = new HashSet<ProjectDeletionParticipantId>();
        for (var index = 0; index < captured.Length; index++) {
            var obligation = obligations[index];
            if (obligation is null || !participants.Add(obligation.Participant)) {
                throw new ArgumentException("Deletion completion requires one exact recovery per participant.", nameof(obligations));
            }
            captured[index] = obligation;
        }
        if (obligations.Count != count) {
            throw new ArgumentException("Deletion obligations changed during capture.", nameof(obligations));
        }
        Original = original;
        Obligations = Array.AsReadOnly(captured);
    }

    public ProjectDeletionPreparationContext Original { get; }
    public IReadOnlyList<ProjectDeletionObligation> Obligations { get; }
}
