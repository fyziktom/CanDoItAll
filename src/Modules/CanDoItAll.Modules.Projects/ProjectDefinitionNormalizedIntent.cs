using System.Text;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionLimitException(string message) : ArgumentException(message);
public sealed class ProjectDefinitionInputException(string message, Exception? inner = null) : ArgumentException(message, inner);

public static class ProjectDefinitionInputBounds {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static void Validate(ProjectDefinitionScalars scalars, IReadOnlyList<ProjectDefinitionPhaseInput>? phases,
        IReadOnlyList<ProjectDefinitionOptionInput>? options, ProjectDefinitionExternalIdentity? externalIdentity,
        ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(scalars);
        ArgumentNullException.ThrowIfNull(limits);
        ValidateFields(phases?.Count ?? 0, options?.Count ?? 0, externalIdentity is not null, Fields(), limits);

        IEnumerable<string?> Fields() {
            yield return scalars.Name;
            yield return scalars.Description;
            yield return scalars.Objective;
            yield return scalars.CurrentPhase;
            if (externalIdentity is not null) {
                yield return externalIdentity.Namespace;
                yield return externalIdentity.Key;
            }
            foreach (var phase in phases ?? []) {
                ArgumentNullException.ThrowIfNull(phase);
                yield return phase.Name;
                yield return phase.Goal;
            }
            foreach (var option in options ?? []) {
                ArgumentNullException.ThrowIfNull(option);
                yield return option.OptionName;
                yield return option.Notes;
            }
        }
    }

    public static void Validate(ProjectEditorModel model, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(model.Phases);
        ArgumentNullException.ThrowIfNull(model.Options);
        ArgumentNullException.ThrowIfNull(limits);
        ValidateFields(model.Phases.Count, model.Options.Count,
            !string.IsNullOrWhiteSpace(model.ExternalNamespace) || !string.IsNullOrWhiteSpace(model.ExternalKey), Fields(), limits);

        IEnumerable<string?> Fields() {
            yield return model.Name;
            yield return model.Description;
            yield return model.Objective;
            yield return model.CurrentPhase;
            yield return model.ExternalNamespace;
            yield return model.ExternalKey;
            foreach (var phase in model.Phases) {
                ArgumentNullException.ThrowIfNull(phase);
                yield return phase.Name;
                yield return phase.Goal;
            }
            foreach (var option in model.Options) {
                ArgumentNullException.ThrowIfNull(option);
                yield return option.OptionName;
                yield return option.Notes;
            }
        }
    }

    private static void ValidateFields(int phaseCount, int optionCount, bool external, IEnumerable<string?> fields, ProjectDefinitionLimits limits) {
        if (phaseCount > limits.MaxPhaseCount || optionCount > limits.MaxOptionCount) {
            throw new ProjectDefinitionLimitException("The definition exceeds its phase or option limit.");
        }
        long total = 1024L + 256L * phaseCount + 192L * optionCount + (external ? 256 : 0);
        RequireTotal();
        foreach (var value in fields) {
            total += Field(value, limits);
            RequireTotal();
        }

        void RequireTotal() {
            if (total > limits.MaxRawRequestBytes) {
                throw new ProjectDefinitionLimitException("The definition's aggregate UTF-8 content and metadata exceed its input budget.");
            }
        }
    }

    internal static int Field(string? value, ProjectDefinitionLimits limits) {
        if (value is null) {
            return 0;
        }
        if (value.Length > limits.MaxFieldUtf8Bytes) {
            throw new ProjectDefinitionLimitException("A definition field exceeds its UTF-8 byte limit.");
        }
        int length;
        try {
            length = StrictUtf8.GetByteCount(value);
        } catch (EncoderFallbackException exception) {
            throw new ProjectDefinitionInputException("A definition field contains malformed UTF-16.", exception);
        }
        if (length > limits.MaxFieldUtf8Bytes) {
            throw new ProjectDefinitionLimitException("A definition field exceeds its UTF-8 byte limit.");
        }
        return length;
    }
}

internal sealed record ProjectDefinitionCreationBinding(Guid? SuppliedProjectId, Guid? ParentProjectId,
    ProjectCreationReservation? Reservation);

internal sealed record ProjectDefinitionNormalizedIntent {
    internal ProjectDefinitionNormalizedIntent(ProjectDefinitionRequestVersion version, ProjectDefinitionCommandKind kind,
        ProjectDefinitionExpectation? expected, ProjectDefinitionScalars scalars,
        IReadOnlyList<ProjectDefinitionPhaseInput>? phases, IReadOnlyList<ProjectDefinitionOptionInput>? options,
        ProjectDefinitionExternalIdentity? externalIdentity, ProjectDefinitionCreationBinding? creation,
        ProjectDefinitionEffectBinding effectBinding, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(effectBinding);
        if (version != ProjectDefinitionRequestVersion.VersionOne) {
            throw new NotSupportedException("The definition request interpretation is not supported.");
        }
        if (!Enum.IsDefined(kind) || kind == ProjectDefinitionCommandKind.Create != (expected is null) ||
            kind == ProjectDefinitionCommandKind.UpdateScalars && (phases is not null || options is not null || externalIdentity is not null || creation is not null) ||
            kind != ProjectDefinitionCommandKind.UpdateScalars && (phases is null || options is null) ||
            kind != ProjectDefinitionCommandKind.Create && creation is not null) {
            throw new ArgumentException("The normalized intent does not match its command kind.");
        }
        ProjectDefinitionInputBounds.Validate(scalars, phases, options, externalIdentity, limits);
        RequireScalars(scalars);
        var phaseIds = new HashSet<Guid>();
        foreach (var phase in phases ?? []) {
            RequireId(phase.Id, phaseIds, kind == ProjectDefinitionCommandKind.Create);
            RequireNormalized(phase.Name, 160);
            RequireNormalized(phase.Goal);
            RequireUtc(phase.StartDateUtc);
            RequireUtc(phase.EndDateUtc);
            if (!Enum.IsDefined(phase.Status)) {
                throw new ArgumentException("A normalized phase status is invalid.");
            }
        }
        var optionIds = new HashSet<Guid>();
        foreach (var option in options ?? []) {
            RequireId(option.Id, optionIds, kind == ProjectDefinitionCommandKind.Create);
            RequireNormalized(option.OptionName, 200);
            RequireNormalized(option.Notes);
            if (!Enum.IsDefined(option.Category)) {
                throw new ArgumentException("A normalized option category is invalid.");
            }
        }
        if (externalIdentity is not null) {
            RequireNormalized(externalIdentity.Namespace);
            RequireNormalized(externalIdentity.Key);
            if (externalIdentity.Namespace.Length == 0 || externalIdentity.Key.Length == 0) {
                throw new ArgumentException("A normalized external identity must be nonempty.");
            }
        }
        ValidateCreation(creation);
        Version = version;
        Kind = kind;
        Expected = expected;
        Scalars = scalars;
        Phases = Array.AsReadOnly(phases?.ToArray() ?? []);
        Options = Array.AsReadOnly(options?.ToArray() ?? []);
        ExternalIdentity = externalIdentity;
        Creation = creation;
        EffectBinding = effectBinding;
    }

    internal ProjectDefinitionRequestVersion Version { get; }
    internal ProjectDefinitionCommandKind Kind { get; }
    internal ProjectDefinitionExpectation? Expected { get; }
    internal ProjectDefinitionScalars Scalars { get; }
    internal IReadOnlyList<ProjectDefinitionPhaseInput> Phases { get; }
    internal IReadOnlyList<ProjectDefinitionOptionInput> Options { get; }
    internal ProjectDefinitionExternalIdentity? ExternalIdentity { get; }
    internal ProjectDefinitionCreationBinding? Creation { get; }
    internal ProjectDefinitionEffectBinding EffectBinding { get; }

    internal static void RequireScalars(ProjectDefinitionScalars scalars) {
        ArgumentNullException.ThrowIfNull(scalars);
        RequireNormalized(scalars.Name, 200);
        RequireNormalized(scalars.Description);
        RequireNormalized(scalars.Objective);
        RequireNormalized(scalars.CurrentPhase, 120);
        RequireUtc(scalars.TargetDateUtc);
        if (scalars.Name.Length == 0 || !Enum.IsDefined(scalars.Status)) {
            throw new ArgumentException("Normalized project scalars require a name and valid status.");
        }
    }

    internal static void RequireNormalized(string value, int maximumLength = int.MaxValue) {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > maximumLength || !value.AsSpan().Trim().SequenceEqual(value)) {
            throw new ArgumentException("The intent must contain already-normalized values.");
        }
    }

    internal static void RequireUtc(DateTime? value) {
        if (value is { Kind: not DateTimeKind.Utc }) {
            throw new ArgumentException("The intent must contain already-normalized UTC timestamps.");
        }
    }

    private static void RequireId(Guid? id, HashSet<Guid> ids, bool creating) {
        if (id.HasValue && (creating || id == Guid.Empty || !ids.Add(id.Value))) {
            throw new ArgumentException("Normalized child identifiers must be nonempty, unique and absent during creation.");
        }
    }

    private static void ValidateCreation(ProjectDefinitionCreationBinding? creation) {
        if (creation is null) {
            return;
        }
        if (creation.SuppliedProjectId == Guid.Empty || creation.ParentProjectId == Guid.Empty) {
            throw new ArgumentException("A supplied creation identity must be nonempty.");
        }
        if (creation.Reservation is not { } reservation) {
            return;
        }
        if (reservation.Id == Guid.Empty || reservation.DatabaseProfileId == Guid.Empty || reservation.ProjectId == Guid.Empty ||
            reservation.LifetimeId == Guid.Empty || reservation.RequesterId == Guid.Empty ||
            reservation.ParentProjectId == Guid.Empty || reservation.ParentLifetimeId == Guid.Empty ||
            reservation.ParentProjectId.HasValue != reservation.ParentLifetimeId.HasValue ||
            creation.SuppliedProjectId != reservation.ProjectId || creation.ParentProjectId != reservation.ParentProjectId) {
            throw new ArgumentException("The creation reservation must match the supplied target and parent identities.");
        }
    }
}
