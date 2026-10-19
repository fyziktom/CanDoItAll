using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectDeletionRecoveryCodec {
    internal const int MaximumEncodedBytes = 65_536;
    private const string Domain = "projects:deletion-recovery:v1";
    private enum SourceKind { NoExternalSourceLease = 1, CanonicalOrganizationCatalog = 2, CanonicalInvocation = 3 }

    internal static byte[] Write(ProjectDeletionRecoveryEvidence evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) {
            writer.WriteStartObject();
            writer.WriteString("domain", Domain);
            writer.WriteNumber("version", 1);
            writer.WriteString("profile", evidence.Original.Completion.DatabaseProfileId);
            writer.WriteString("namespace", evidence.Original.Completion.Namespace.Value);
            writer.WriteString("completion", evidence.Original.Completion.Id.Value);
            writer.WritePropertyName("project");
            WriteProject(writer, evidence.Original.Project);
            writer.WritePropertyName("origin");
            WriteOrigin(writer, evidence.Origin);
            writer.WritePropertyName("source");
            WriteSource(writer, evidence.Source);
            writer.WriteStartArray("requiredProjects");
            foreach (var project in evidence.RequiredProjects) {
                WriteProject(writer, project);
            }
            writer.WriteEndArray();
            if (!evidence.PreparationParticipants.IsDefault) {
                writer.WriteStartArray("preparationParticipants");
                foreach (var participant in evidence.PreparationParticipants) {
                    writer.WriteStartObject();
                    writer.WriteString("id", participant.Id.Value);
                    writer.WriteNumber("protocol", participant.ProtocolVersion);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        if (output.Length > MaximumEncodedBytes) {
            throw new InvalidDataException("Deletion recovery exceeds its complete encoded byte bound.");
        }
        return output.ToArray();
    }

    internal static ProjectDeletionRecoveryEvidence Read(ReadOnlySpan<byte> encoded,
        ProjectDeletionPreparationContext expected) {
        ArgumentNullException.ThrowIfNull(expected);
        if (encoded.IsEmpty || encoded.Length > MaximumEncodedBytes) {
            throw new InvalidDataException("Deletion recovery is empty or exceeds its encoded byte bound.");
        }
        try {
            using var document = JsonDocument.Parse(encoded.ToArray(), new() { MaxDepth = 6 });
            var root = document.RootElement;
            if (root.GetProperty("domain").GetString() != Domain || root.GetProperty("version").GetInt32() != 1) {
                throw new InvalidDataException("The deletion recovery interpretation is unsupported.");
            }
            var original = new ProjectDeletionPreparationContext(new(root.GetProperty("profile").GetGuid(),
                new(root.GetProperty("namespace").GetGuid()), new(root.GetProperty("completion").GetGuid())),
                ReadProject(root.GetProperty("project")));
            if (original != expected) {
                throw new InvalidDataException("Deletion recovery changed its original completion or project lifetime.");
            }
            var projects = root.GetProperty("requiredProjects");
            if (projects.GetArrayLength() is < 1 or > ProjectDeletionRecoveryEvidence.MaximumProjects) {
                throw new InvalidDataException("Deletion recovery exceeds its project bound.");
            }
            var evidence = new ProjectDeletionRecoveryEvidence(original, ReadOrigin(root.GetProperty("origin"), original),
                ReadSource(root.GetProperty("source")), projects.EnumerateArray().Select(ReadProject).ToArray(), ReadPreparation(root));
            if (!encoded.SequenceEqual(Write(evidence))) {
                throw new InvalidDataException("Deletion recovery is not its exact canonical representation.");
            }
            return evidence;
        } catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or
                FormatException or KeyNotFoundException or OverflowException) {
            throw new InvalidDataException("The retained deletion recovery evidence is invalid.", failure);
        }
    }

    private static void WriteProject(Utf8JsonWriter writer, ProjectWriteAdmission project) {
        writer.WriteStartObject();
        writer.WriteString("profile", project.DatabaseProfileId);
        writer.WriteString("project", project.ProjectId);
        writer.WriteString("lifetime", project.LifetimeId);
        writer.WriteEndObject();
    }

    private static ProjectDeletionPreparationRegistration[]? ReadPreparation(JsonElement root) {
        if (!root.TryGetProperty("preparationParticipants", out JsonElement participants)) {
            return null;
        }
        if (participants.GetArrayLength() > ProjectDeletionCompletionBinding.MaximumParticipants) {
            throw new InvalidDataException("Deletion recovery exceeds its preparation registration bound.");
        }
        return participants.EnumerateArray().Select(value => new ProjectDeletionPreparationRegistration(
            new(value.GetProperty("id").GetString()!), value.GetProperty("protocol").GetInt32())).ToArray();
    }

    private static ProjectWriteAdmission ReadProject(JsonElement value)
        => new(value.GetProperty("profile").GetGuid(), value.GetProperty("project").GetGuid(), value.GetProperty("lifetime").GetGuid());

    private static void WriteOrigin(Utf8JsonWriter writer, ProjectDeletionOriginEvidence origin) {
        writer.WriteStartObject();
        switch (origin) {
            case ProjectDeletionOriginEvidence.Ordinary ordinary:
                writer.WriteNumber("kind", (int)ProjectDeletionCompletionOrigin.OrdinaryDeletion);
                writer.WriteNumber("schema", ordinary.Submission.Interpretation.Schema);
                writer.WriteNumber("normalization", ordinary.Submission.Interpretation.Normalization);
                writer.WriteNumber("epoch", ordinary.Submission.Operation.Epoch.Value);
                writer.WriteString("caller", ordinary.Submission.Operation.CallerBinding.Value);
                if (ordinary.CallerExclusion is { } caller) {
                    writer.WriteStartObject("callerExclusion");
                    writer.WriteNumber("kind", (int)caller.Kind);
                    writer.WriteString("key", caller.Key.Hex);
                    writer.WriteEndObject();
                }
                writer.WriteString("operation", ordinary.Submission.Operation.Id);
                writer.WriteString("revision", ((ProjectLifecycleCommand.Delete)ordinary.Submission.Command).Expected.Revision);
                break;
            case ProjectDeletionOriginEvidence.Compensation compensation:
                writer.WriteNumber("kind", (int)ProjectDeletionCompletionOrigin.CreationCompensation);
                writer.WriteString("fingerprint", compensation.Fingerprint);
                if (compensation.Reservation is not { } reservation) {
                    writer.WriteNull("reservation");
                    break;
                }
                writer.WriteStartObject("reservation");
                writer.WriteString("id", reservation.Id);
                writer.WriteString("requester", reservation.RequesterId);
                if (reservation.ParentProjectId is { } parent) {
                    writer.WriteString("parent", parent);
                    writer.WriteString("parentLifetime", reservation.ParentLifetimeId!.Value);
                } else {
                    writer.WriteNull("parent");
                    writer.WriteNull("parentLifetime");
                }
                writer.WriteEndObject();
                break;
            case ProjectDeletionOriginEvidence.ReservedCancellation cancellation:
                writer.WriteNumber("kind", (int)ProjectDeletionCompletionOrigin.ReservedCancellation);
                writer.WriteString("reservation", cancellation.Target.ReservationId);
                writer.WriteString("requester", cancellation.Target.RequesterId);
                if (cancellation.Target.Parent is { } cancelledParent) {
                    writer.WriteString("parent", cancelledParent.ProjectId);
                    writer.WriteString("parentLifetime", cancelledParent.LifetimeId);
                } else {
                    writer.WriteNull("parent");
                    writer.WriteNull("parentLifetime");
                }
                break;
            case ProjectDeletionOriginEvidence.LegacyInstallation legacy:
                writer.WriteNumber("kind", (int)ProjectDeletionCompletionOrigin.LegacyInstallation);
                writer.WriteString("plan", legacy.InstallationPlanDigest);
                if (legacy.Precondition is { } precondition) {
                    writer.WriteNumber("installationPrecondition", (int)precondition);
                }
                break;
            default:
                throw new InvalidDataException("Unsupported deletion recovery origin.");
        }
        writer.WriteEndObject();
    }

    private static ProjectDeletionOriginEvidence ReadOrigin(JsonElement value, ProjectDeletionPreparationContext original) {
        var project = original.Project;
        switch ((ProjectDeletionCompletionOrigin)value.GetProperty("kind").GetInt32()) {
            case ProjectDeletionCompletionOrigin.OrdinaryDeletion:
                return ReadOrdinary(value, original);
            case ProjectDeletionCompletionOrigin.CreationCompensation:
                var reservation = value.GetProperty("reservation");
                ProjectCreationReservation? captured = reservation.ValueKind == JsonValueKind.Null ? null : new(
                    reservation.GetProperty("id").GetGuid(), project.DatabaseProfileId, project.ProjectId, project.LifetimeId,
                    reservation.GetProperty("requester").GetGuid(), ReadOptionalGuid(reservation.GetProperty("parent")),
                    ReadOptionalGuid(reservation.GetProperty("parentLifetime")));
                return new ProjectDeletionOriginEvidence.Compensation(project, value.GetProperty("fingerprint").GetString()!, captured);
            case ProjectDeletionCompletionOrigin.ReservedCancellation:
                var parentId = ReadOptionalGuid(value.GetProperty("parent"));
                var parentLifetime = ReadOptionalGuid(value.GetProperty("parentLifetime"));
                if (parentId.HasValue != parentLifetime.HasValue) {
                    throw new InvalidDataException("Reserved cancellation has an incomplete original parent lifetime.");
                }
                return new ProjectDeletionOriginEvidence.ReservedCancellation(new(project,
                    value.GetProperty("reservation").GetGuid(), value.GetProperty("requester").GetGuid(),
                    parentId is { } parent ? new(project.DatabaseProfileId, parent, parentLifetime!.Value) : null));
            case ProjectDeletionCompletionOrigin.LegacyInstallation:
                return new ProjectDeletionOriginEvidence.LegacyInstallation(value.GetProperty("plan").GetString()!,
                    value.TryGetProperty("installationPrecondition", out var precondition)
                        ? (ProjectDeletionLegacyPrecondition)precondition.GetInt32() : null);
            default:
                throw new InvalidDataException("Unsupported deletion recovery origin.");
        }
    }

    private static Guid? ReadOptionalGuid(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();

    private static ProjectDeletionOriginEvidence.Ordinary ReadOrdinary(JsonElement value, ProjectDeletionPreparationContext original) {
        ProjectDeletionCallerExclusion? exclusion = null;
        if (value.TryGetProperty("callerExclusion", out var caller)) {
            var key = caller.GetProperty("key").GetString();
            if (key is not { Length: 64 } || key.AsSpan().ContainsAnyExcept("0123456789abcdef")) {
                throw new InvalidDataException("Deletion recovery caller exclusion requires an exact lowercase 32-byte key.");
            }
            exclusion = new((ProjectMutationCallerKind)caller.GetProperty("kind").GetInt32(), new(Convert.FromHexString(key)));
        }
        return new(new(new(original.Completion.DatabaseProfileId, original.Completion.Namespace,
            new(value.GetProperty("epoch").GetInt64()), new(value.GetProperty("caller").GetString()!), value.GetProperty("operation").GetGuid()),
            new(value.GetProperty("schema").GetInt32(), value.GetProperty("normalization").GetInt32()),
            new ProjectLifecycleCommand.Delete(new(original.Project, value.GetProperty("revision").GetGuid()))), exclusion);
    }

    private static void WriteSource(Utf8JsonWriter writer, ProjectDeletionSourceRecovery source) {
        writer.WriteStartObject();
        switch (source) {
            case ProjectDeletionSourceRecovery.NoExternalSourceLease:
                writer.WriteNumber("kind", (int)SourceKind.NoExternalSourceLease);
                break;
            case ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog:
                writer.WriteNumber("kind", (int)SourceKind.CanonicalOrganizationCatalog);
                writer.WriteString("profile", catalog.DatabaseProfileId);
                WriteLocation(writer, catalog.Location);
                break;
            case ProjectDeletionSourceRecovery.CanonicalInvocation invocation:
                writer.WriteNumber("kind", (int)SourceKind.CanonicalInvocation);
                writer.WritePropertyName("catalog");
                WriteSource(writer, invocation.Catalog);
                writer.WriteStartObject("execution");
                WriteLocation(writer, invocation.Execution);
                writer.WriteNumber("scope", (int)invocation.Scope);
                writer.WriteString("key", invocation.Key);
                writer.WriteEndObject();
                break;
            default:
                throw new InvalidDataException("Unsupported deletion recovery source.");
        }
        writer.WriteEndObject();
    }

    private static ProjectDeletionSourceRecovery ReadSource(JsonElement value) => (SourceKind)value.GetProperty("kind").GetInt32() switch {
        SourceKind.NoExternalSourceLease => new ProjectDeletionSourceRecovery.NoExternalSourceLease(),
        SourceKind.CanonicalOrganizationCatalog => new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(
            value.GetProperty("profile").GetGuid(), ReadLocation(value).ToPathRecord()),
        SourceKind.CanonicalInvocation => ReadInvocation(value),
        _ => throw new InvalidDataException("Unsupported deletion recovery source.")
    };

    private static ProjectDeletionSourceRecovery.CanonicalInvocation ReadInvocation(JsonElement value) {
        var catalog = value.GetProperty("catalog");
        if ((SourceKind)catalog.GetProperty("kind").GetInt32() != SourceKind.CanonicalOrganizationCatalog) {
            throw new InvalidDataException("Invocation recovery requires exactly one original canonical catalog.");
        }
        var execution = value.GetProperty("execution");
        return new((ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog)ReadSource(catalog), ReadLocation(execution),
            (ProjectDeletionExecutionWorkspaceKind)execution.GetProperty("scope").GetInt32(), execution.GetProperty("key").GetString()!);
    }

    private static void WriteLocation(Utf8JsonWriter writer, ProjectDeletionWorkspaceLocation location) {
        writer.WriteNumber("format", location.FormatVersion);
        writer.WriteNumber("platform", (int)location.PlatformFamily);
        writer.WriteNumber("syntax", (int)location.PathSyntax);
        writer.WriteString("host", location.HostBindingId);
        writer.WriteString("root", location.RootPath);
        writer.WriteNumber("state", (int)location.State);
    }

    private static ProjectDeletionWorkspaceLocation ReadLocation(JsonElement value) => new(new() {
        FormatVersion = value.GetProperty("format").GetInt32(),
        PlatformFamily = (HostPlatformFamily)value.GetProperty("platform").GetInt32(),
        PathSyntax = (PhysicalPathSyntax)value.GetProperty("syntax").GetInt32(),
        HostBindingId = value.GetProperty("host").GetString()!,
        Path = value.GetProperty("root").GetString()!,
        State = (HostBoundPathState)value.GetProperty("state").GetInt32()
    });
}
