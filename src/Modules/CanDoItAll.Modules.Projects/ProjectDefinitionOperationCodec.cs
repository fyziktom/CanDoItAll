using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDefinitionDigest {
    internal ProjectDefinitionDigest(ReadOnlySpan<byte> bytes) {
        if (bytes.Length != 32) {
            throw new ArgumentException("A semantic digest requires 32 bytes.", nameof(bytes));
        }
        Hex = Convert.ToHexStringLower(bytes);
    }

    internal string Hex { get; }
    internal byte[] ToArray() => Convert.FromHexString(Hex);
}

internal sealed class ProjectDefinitionEncodedResult {
    private readonly byte[] bytes;

    internal ProjectDefinitionEncodedResult(ReadOnlySpan<byte> bytes) {
        this.bytes = bytes.ToArray();
    }

    internal int Length => bytes.Length;
    internal byte[] ToArray() => bytes.ToArray();
}

internal abstract record ProjectDefinitionDecodedReceipt {
    private ProjectDefinitionDecodedReceipt() {
    }

    internal sealed record Full(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Result) : ProjectDefinitionDecodedReceipt;
    internal sealed record Scalars(ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> Result) : ProjectDefinitionDecodedReceipt;
}

internal sealed class ProjectDefinitionReceiptDecodeException(string message, Exception? inner = null) : Exception(message, inner);

internal static class ProjectDefinitionOperationCodec {
    internal static ProjectDefinitionDigest ComputeDigest(ProjectDefinitionNormalizedIntent intent,
        ProjectDefinitionOperation operation, ProjectMutationCallerScope caller, ProjectDefinitionLimits limits)
        => ComputeDigest(intent, operation, caller, limits, out _);

    internal static ProjectDefinitionDigest ComputeDigest(ProjectDefinitionNormalizedIntent intent,
        ProjectDefinitionOperation operation, ProjectMutationCallerScope caller, ProjectDefinitionLimits limits, out int canonicalByteLength) {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(limits);
        if (!caller.Matches(operation.Namespace, operation.CallerBinding)) {
            throw new ArgumentException("The original caller binding does not match the trusted invocation.", nameof(operation));
        }
        if (caller.Kind == ProjectMutationCallerKind.GovernedExecution && intent.EffectBinding.Kind == ProjectDefinitionEffectKind.None) {
            throw new ArgumentException("A governed definition write requires its admitted effect binding.", nameof(intent));
        }
        var bytes = Encode(limits.MaxCanonicalCommandBytes, writer => {
            writer.WriteStartObject();
            writer.WriteString("domain", "projects:definition-intent:v1");
            writer.WriteNumber("schema", intent.Version.Schema);
            writer.WriteNumber("normalization", intent.Version.Normalization);
            writer.WriteNumber("kind", (int)intent.Kind);
            WriteOperation(writer, operation);
            writer.WriteNumber("callerKind", (int)caller.Kind);
            writer.WriteString("callerKey", caller.Key.Hex);
            writer.WritePropertyName("expected");
            WriteExpectation(writer, intent.Expected);
            WriteScalars(writer, intent.Scalars);
            WriteExternalIdentity(writer, intent.ExternalIdentity);
            writer.WriteStartArray("phases");
            foreach (var phase in intent.Phases) {
                WritePhase(writer, phase.Id, phase.Name, phase.Goal, phase.Status, phase.StartDateUtc, phase.EndDateUtc);
            }
            writer.WriteEndArray();
            writer.WriteStartArray("options");
            foreach (var option in intent.Options) {
                WriteOption(writer, option.Id, option.Category, option.OptionName, option.Notes);
            }
            writer.WriteEndArray();
            WriteCreation(writer, intent.Creation);
            writer.WriteStartObject("effect");
            writer.WriteNumber("kind", (int)intent.EffectBinding.Kind);
            writer.WriteNumber("version", intent.EffectBinding.Version);
            writer.WriteString("digest", intent.EffectBinding.Hex);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        canonicalByteLength = bytes.Length;
        return new(SHA256.HashData(bytes));
    }

    internal static ProjectDefinitionEncodedResult EncodeAccepted(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> result,
        ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(result);
        ValidateFull(result.Acknowledgement, limits);
        return new(Encode(limits.MaxAcceptedResultBytes, writer => {
            WriteResultStart(writer, result.Operation, result.Version, result.Kind, result.AcceptedAtUtc, result.ReplayUntilUtc);
            var acknowledgement = result.Acknowledgement;
            writer.WriteStartObject("acknowledgement");
            writer.WritePropertyName("definition");
            WriteExpectation(writer, acknowledgement.Definition);
            WriteScalars(writer, Scalars(acknowledgement));
            WriteExternalIdentity(writer, acknowledgement.ExternalIdentity);
            writer.WriteStartArray("phases");
            foreach (var phase in acknowledgement.Phases) {
                WritePhase(writer, phase.Id, phase.Name, phase.Goal, phase.Status, phase.StartDateUtc, phase.EndDateUtc, phase.SubmittedIndex);
            }
            writer.WriteEndArray();
            writer.WriteStartArray("options");
            foreach (var option in acknowledgement.Options) {
                WriteOption(writer, option.Id, option.Category, option.OptionName, option.Notes, option.SubmittedIndex);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }));
    }

    internal static ProjectDefinitionEncodedResult EncodeAccepted(ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> result,
        ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(result);
        ValidateScalars(result.Acknowledgement, limits);
        return new(Encode(limits.MaxAcceptedResultBytes, writer => {
            WriteResultStart(writer, result.Operation, result.Version, result.Kind, result.AcceptedAtUtc, result.ReplayUntilUtc);
            writer.WriteStartObject("acknowledgement");
            writer.WritePropertyName("definition");
            WriteExpectation(writer, result.Acknowledgement.Definition);
            WriteScalars(writer, result.Acknowledgement.Scalars);
            WriteExternalIdentity(writer, result.Acknowledgement.ExternalIdentity);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }));
    }

    internal static ProjectDefinitionDecodedReceipt DecodeAccepted(ProjectDefinitionResultVersion version,
        ReadOnlySpan<byte> bytes, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(limits);
        if (version != ProjectDefinitionResultVersion.VersionOne) {
            throw new NotSupportedException("The accepted result codec version is not supported.");
        }
        if (bytes.Length == 0 || bytes.Length > limits.MaxAcceptedResultBytes) {
            throw new ProjectDefinitionReceiptDecodeException("The stored accepted result exceeds its supported byte bounds.");
        }
        try {
            using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 16 });
            var root = document.RootElement;
            if (root.GetProperty("resultVersion").GetInt32() != version.Value) {
                throw new JsonException("The stored result version does not match its receipt.");
            }
            var kind = (ProjectDefinitionCommandKind)root.GetProperty("kind").GetInt32();
            var operation = ReadOperation(root.GetProperty("operation"));
            var acceptedAtUtc = root.GetProperty("acceptedAtUtc").GetDateTimeOffset();
            var replayUntilUtc = root.GetProperty("replayUntilUtc").GetDateTimeOffset();
            var acknowledgement = root.GetProperty("acknowledgement");
            var definition = ReadExpectation(acknowledgement.GetProperty("definition"));
            var scalars = ReadScalars(acknowledgement.GetProperty("scalars"));
            var externalIdentity = ReadExternalIdentity(acknowledgement.GetProperty("externalIdentity"));
            ProjectDefinitionEncodedResult canonical;
            ProjectDefinitionDecodedReceipt decoded;
            if (kind == ProjectDefinitionCommandKind.UpdateScalars) {
                var scalar = new ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement>(operation, version, kind,
                    acceptedAtUtc, replayUntilUtc, new(definition, scalars, externalIdentity));
                canonical = EncodeAccepted(scalar, limits);
                decoded = new ProjectDefinitionDecodedReceipt.Scalars(scalar);
            } else if (kind is ProjectDefinitionCommandKind.Create or ProjectDefinitionCommandKind.Replace) {
                var phases = ReadPhases(acknowledgement.GetProperty("phases"), limits);
                var options = ReadOptions(acknowledgement.GetProperty("options"), limits);
                var full = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(operation, version, kind,
                    acceptedAtUtc, replayUntilUtc, new(definition, scalars.Name, scalars.Description, scalars.Objective,
                        scalars.Status, scalars.CurrentPhase, scalars.TargetDateUtc, phases, options, externalIdentity));
                canonical = EncodeAccepted(full, limits);
                decoded = new ProjectDefinitionDecodedReceipt.Full(full);
            } else {
                throw new JsonException("The stored command kind is unsupported.");
            }
            if (!bytes.SequenceEqual(canonical.ToArray())) {
                throw new JsonException("The stored result is not the canonical accepted payload.");
            }
            return decoded;
        } catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or
            FormatException or KeyNotFoundException or OverflowException) {
            throw new ProjectDefinitionReceiptDecodeException("The stored accepted result is invalid.", exception);
        }
    }

    private static byte[] Encode(int maximumBytes, Action<Utf8JsonWriter> write) {
        using var stream = new BoundedStream(maximumBytes);
        using var writer = new Utf8JsonWriter(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteResultStart(Utf8JsonWriter writer, ProjectDefinitionOperation operation,
        ProjectDefinitionResultVersion version, ProjectDefinitionCommandKind kind, DateTimeOffset acceptedAtUtc,
        DateTimeOffset replayUntilUtc) {
        if (version != ProjectDefinitionResultVersion.VersionOne) {
            throw new NotSupportedException("The accepted result codec version is not supported.");
        }
        writer.WriteStartObject();
        writer.WriteNumber("resultVersion", version.Value);
        writer.WriteNumber("kind", (int)kind);
        WriteOperation(writer, operation);
        writer.WriteString("acceptedAtUtc", acceptedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("replayUntilUtc", replayUntilUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void WriteOperation(Utf8JsonWriter writer, ProjectDefinitionOperation operation) {
        writer.WriteStartObject("operation");
        writer.WriteString("namespace", operation.Namespace.Value);
        writer.WriteNumber("epoch", operation.Epoch.Value);
        writer.WriteString("id", operation.Id);
        writer.WriteString("callerBinding", operation.CallerBinding.Value);
        writer.WriteEndObject();
    }

    private static void WriteExpectation(Utf8JsonWriter writer, ProjectDefinitionExpectation? expectation) {
        if (expectation is null) {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("databaseProfileId", expectation.Project.DatabaseProfileId);
        writer.WriteString("projectId", expectation.Project.ProjectId);
        writer.WriteString("lifetimeId", expectation.Project.LifetimeId);
        writer.WriteString("revision", expectation.Revision);
        writer.WriteEndObject();
    }

    private static void WriteScalars(Utf8JsonWriter writer, ProjectDefinitionScalars scalars) {
        writer.WriteStartObject("scalars");
        writer.WriteString("name", scalars.Name);
        writer.WriteString("description", scalars.Description);
        writer.WriteString("objective", scalars.Objective);
        writer.WriteNumber("status", (int)scalars.Status);
        writer.WriteString("currentPhase", scalars.CurrentPhase);
        WriteDate(writer, "targetDateUtc", scalars.TargetDateUtc);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteExternalIdentity(Utf8JsonWriter writer, ProjectDefinitionExternalIdentity? identity) {
        writer.WritePropertyName("externalIdentity");
        if (identity is null) {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("namespace", identity.Namespace);
        writer.WriteString("key", identity.Key);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WritePhase(Utf8JsonWriter writer, Guid? id, string name, string goal, ProjectPhaseStatus status,
        DateTime? startDateUtc, DateTime? endDateUtc, int? submittedIndex = null) {
        writer.WriteStartObject();
        if (submittedIndex.HasValue) {
            writer.WriteNumber("submittedIndex", submittedIndex.Value);
        }
        WriteGuid(writer, "id", id);
        writer.WriteString("name", name);
        writer.WriteString("goal", goal);
        writer.WriteNumber("status", (int)status);
        WriteDate(writer, "startDateUtc", startDateUtc);
        WriteDate(writer, "endDateUtc", endDateUtc);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteOption(Utf8JsonWriter writer, Guid? id, ProjectOptionCategory category, string name,
        string notes, int? submittedIndex = null) {
        writer.WriteStartObject();
        if (submittedIndex.HasValue) {
            writer.WriteNumber("submittedIndex", submittedIndex.Value);
        }
        WriteGuid(writer, "id", id);
        writer.WriteNumber("category", (int)category);
        writer.WriteString("optionName", name);
        writer.WriteString("notes", notes);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteCreation(Utf8JsonWriter writer, ProjectDefinitionCreationBinding? creation) {
        writer.WritePropertyName("creation");
        if (creation is null) {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        WriteGuid(writer, "suppliedProjectId", creation.SuppliedProjectId);
        WriteGuid(writer, "parentProjectId", creation.ParentProjectId);
        writer.WritePropertyName("reservation");
        if (creation.Reservation is { } reservation) {
            writer.WriteStartObject();
            writer.WriteString("id", reservation.Id);
            writer.WriteString("databaseProfileId", reservation.DatabaseProfileId);
            writer.WriteString("projectId", reservation.ProjectId);
            writer.WriteString("lifetimeId", reservation.LifetimeId);
            writer.WriteString("requesterId", reservation.RequesterId);
            WriteGuid(writer, "parentProjectId", reservation.ParentProjectId);
            WriteGuid(writer, "parentLifetimeId", reservation.ParentLifetimeId);
            writer.WriteEndObject();
        } else {
            writer.WriteNullValue();
        }
        writer.WriteEndObject();
    }

    private static void WriteGuid(Utf8JsonWriter writer, string name, Guid? value) {
        if (value.HasValue) {
            writer.WriteString(name, value.Value);
        } else {
            writer.WriteNull(name);
        }
    }

    private static void WriteDate(Utf8JsonWriter writer, string name, DateTime? value) {
        if (value.HasValue) {
            writer.WriteString(name, value.Value.ToString("O", CultureInfo.InvariantCulture));
        } else {
            writer.WriteNull(name);
        }
    }

    private static ProjectDefinitionOperation ReadOperation(JsonElement value)
        => new(new(value.GetProperty("namespace").GetGuid()), new(value.GetProperty("epoch").GetInt64()),
            value.GetProperty("id").GetGuid(), new(value.GetProperty("callerBinding").GetString()!));

    private static ProjectDefinitionExpectation ReadExpectation(JsonElement value)
        => new(new(value.GetProperty("databaseProfileId").GetGuid(), value.GetProperty("projectId").GetGuid(),
            value.GetProperty("lifetimeId").GetGuid()), value.GetProperty("revision").GetGuid());

    private static ProjectDefinitionScalars ReadScalars(JsonElement value)
        => new(value.GetProperty("name").GetString()!, value.GetProperty("description").GetString()!,
            value.GetProperty("objective").GetString()!, (ProjectStatus)value.GetProperty("status").GetInt32(),
            value.GetProperty("currentPhase").GetString()!, ReadDate(value, "targetDateUtc"));

    private static ProjectDefinitionExternalIdentity? ReadExternalIdentity(JsonElement value)
        => value.ValueKind == JsonValueKind.Null ? null : new(value.GetProperty("namespace").GetString()!, value.GetProperty("key").GetString()!);

    private static DateTime? ReadDate(JsonElement value, string property)
        => value.GetProperty(property).ValueKind == JsonValueKind.Null ? null : value.GetProperty(property).GetDateTime();

    private static IReadOnlyList<ProjectPhaseAcknowledgement> ReadPhases(JsonElement value, ProjectDefinitionLimits limits) {
        if (value.GetArrayLength() > limits.MaxPhaseCount) {
            throw new JsonException("The accepted phase count exceeds its supported limit.");
        }
        var result = new List<ProjectPhaseAcknowledgement>(value.GetArrayLength());
        foreach (var phase in value.EnumerateArray()) {
            result.Add(new(phase.GetProperty("submittedIndex").GetInt32(), phase.GetProperty("id").GetGuid(),
                phase.GetProperty("name").GetString()!, phase.GetProperty("goal").GetString()!,
                (ProjectPhaseStatus)phase.GetProperty("status").GetInt32(), ReadDate(phase, "startDateUtc"), ReadDate(phase, "endDateUtc")));
        }
        return result;
    }

    private static IReadOnlyList<ProjectOptionAcknowledgement> ReadOptions(JsonElement value, ProjectDefinitionLimits limits) {
        if (value.GetArrayLength() > limits.MaxOptionCount) {
            throw new JsonException("The accepted option count exceeds its supported limit.");
        }
        var result = new List<ProjectOptionAcknowledgement>(value.GetArrayLength());
        foreach (var option in value.EnumerateArray()) {
            result.Add(new(option.GetProperty("submittedIndex").GetInt32(), option.GetProperty("id").GetGuid(),
                (ProjectOptionCategory)option.GetProperty("category").GetInt32(), option.GetProperty("optionName").GetString()!,
                option.GetProperty("notes").GetString()!));
        }
        return result;
    }

    private static ProjectDefinitionScalars Scalars(ProjectEditorAcknowledgement value)
        => new(value.Name, value.Description, value.Objective, value.Status, value.CurrentPhase, value.TargetDateUtc);

    private static void ValidateScalars(ProjectDefinitionScalarsAcknowledgement value, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(value.Definition);
        ProjectDefinitionInputBounds.Validate(value.Scalars, null, null, value.ExternalIdentity, limits);
        ProjectDefinitionNormalizedIntent.RequireScalars(value.Scalars);
        ValidateExternalIdentity(value.ExternalIdentity);
    }

    private static void ValidateFull(ProjectEditorAcknowledgement value, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(value.Definition);
        ArgumentNullException.ThrowIfNull(value.Phases);
        ArgumentNullException.ThrowIfNull(value.Options);
        if (value.Phases.Count > limits.MaxPhaseCount || value.Options.Count > limits.MaxOptionCount) {
            throw new ProjectDefinitionLimitException("The accepted definition exceeds its child count limit.");
        }
        ValidateScalars(new(value.Definition, Scalars(value), value.ExternalIdentity), limits);
        var phaseIds = new HashSet<Guid>();
        var lastPhaseIndex = -1;
        foreach (var phase in value.Phases) {
            ArgumentNullException.ThrowIfNull(phase);
            if (phase.SubmittedIndex != lastPhaseIndex + 1) {
                throw new ArgumentException("Accepted phase mappings must preserve every submitted slot.");
            }
            ValidateMapping(phase.SubmittedIndex, phase.Id, ref lastPhaseIndex, phaseIds, limits.MaxPhaseCount);
            ProjectDefinitionInputBounds.Field(phase.Name, limits);
            ProjectDefinitionInputBounds.Field(phase.Goal, limits);
            ProjectDefinitionNormalizedIntent.RequireNormalized(phase.Name, 160);
            ProjectDefinitionNormalizedIntent.RequireNormalized(phase.Goal);
            ProjectDefinitionNormalizedIntent.RequireUtc(phase.StartDateUtc);
            ProjectDefinitionNormalizedIntent.RequireUtc(phase.EndDateUtc);
            if (!Enum.IsDefined(phase.Status)) {
                throw new ArgumentException("The accepted phase status is invalid.");
            }
        }
        var optionIds = new HashSet<Guid>();
        var lastOptionIndex = -1;
        foreach (var option in value.Options) {
            ArgumentNullException.ThrowIfNull(option);
            ValidateMapping(option.SubmittedIndex, option.Id, ref lastOptionIndex, optionIds, limits.MaxOptionCount);
            ProjectDefinitionInputBounds.Field(option.OptionName, limits);
            ProjectDefinitionInputBounds.Field(option.Notes, limits);
            ProjectDefinitionNormalizedIntent.RequireNormalized(option.OptionName, 200);
            ProjectDefinitionNormalizedIntent.RequireNormalized(option.Notes);
            if (!Enum.IsDefined(option.Category) || option.OptionName.Length == 0 && option.Notes.Length == 0) {
                throw new ArgumentException("The accepted option is absent or has an invalid category.");
            }
        }
    }

    private static void ValidateMapping(int index, Guid id, ref int previous, HashSet<Guid> ids, int maximum) {
        if (index <= previous || index >= maximum || id == Guid.Empty || !ids.Add(id)) {
            throw new ArgumentException("Accepted child mappings must have increasing bounded indices and unique nonempty identifiers.");
        }
        previous = index;
    }

    private static void ValidateExternalIdentity(ProjectDefinitionExternalIdentity? identity) {
        if (identity is null) {
            return;
        }
        ProjectDefinitionNormalizedIntent.RequireNormalized(identity.Namespace);
        ProjectDefinitionNormalizedIntent.RequireNormalized(identity.Key);
        if (identity.Namespace.Length == 0 || identity.Key.Length == 0) {
            throw new ArgumentException("The accepted external identity must be nonempty.");
        }
    }

    private sealed class BoundedStream(int maximumBytes) : MemoryStream {
        public override void Write(byte[] buffer, int offset, int count) {
            RequireCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            RequireCapacity(buffer.Length);
            base.Write(buffer);
        }

        private void RequireCapacity(int count) {
            if (count > maximumBytes - Length) {
                throw new ProjectDefinitionLimitException("The canonical definition payload exceeds its byte limit.");
            }
            var required = (int)Length + count;
            if (required > Capacity) {
                Capacity = (int)Math.Min(maximumBytes, Math.Max(required, Math.Max(256L, 2L * Capacity)));
            }
        }
    }
}
