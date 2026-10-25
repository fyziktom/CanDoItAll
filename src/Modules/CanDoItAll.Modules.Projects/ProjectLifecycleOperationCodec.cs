using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectLifecycleOperationCodec {
    private const string IntentDomain = "projects:lifecycle-intent:v1";
    private const string ResultDomain = "projects:lifecycle-result:v1";
    private const string SubmissionDomain = "projects:lifecycle-submission:v1";
    private const int MaximumIntentBytes = 4096;

    internal static ProjectLifecycleDigest ComputeDigest(ProjectLifecycleSubmission original, ProjectMutationCallerScope caller,
        ProjectLifecycleEffectBinding effect) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(effect);
        RequireVersion(original.Interpretation);
        if (!caller.Matches(original.Operation.Namespace, original.Operation.CallerBinding) ||
                (caller.Kind == ProjectMutationCallerKind.GovernedExecution) == (effect.Kind == ProjectLifecycleEffectKind.None)) {
            throw new ArgumentException("Lifecycle intent requires its original trusted caller and exact governed effect digest.");
        }
        var bytes = Encode(MaximumIntentBytes, writer => {
            writer.WriteStartObject();
            writer.WriteString("domain", IntentDomain);
            WriteSubmission(writer, original);
            writer.WriteNumber("callerKind", (int)caller.Kind);
            writer.WriteString("callerKey", caller.Key.Hex);
            writer.WriteStartObject("effect");
            writer.WriteNumber("kind", (int)effect.Kind);
            writer.WriteNumber("version", effect.Version);
            writer.WriteString("digest", effect.Hex);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        return new(SHA256.HashData(bytes));
    }

    internal static byte[] EncodeSubmission(ProjectLifecycleSubmission original) {
        ArgumentNullException.ThrowIfNull(original);
        return Encode(MaximumIntentBytes, writer => {
            writer.WriteStartObject();
            writer.WriteString("domain", SubmissionDomain);
            WriteSubmission(writer, original);
            writer.WriteEndObject();
        });
    }

    internal static ProjectLifecycleSubmission DecodeSubmission(ReadOnlySpan<byte> bytes) {
        if (bytes.Length is 0 or > MaximumIntentBytes) {
            throw new InvalidDataException("The pending lifecycle submission exceeds its supported byte bound.");
        }
        try {
            using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.GetProperty("domain").GetString() != SubmissionDomain) {
                throw new InvalidDataException("The pending lifecycle submission has an unsupported domain.");
            }
            var original = ReadSubmission(root);
            if (!bytes.SequenceEqual(EncodeSubmission(original))) {
                throw new InvalidDataException("The pending lifecycle submission is not its exact canonical encoding.");
            }
            return original;
        } catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or FormatException or KeyNotFoundException or NotSupportedException or OverflowException) {
            throw new InvalidDataException("The pending lifecycle submission is invalid.", failure);
        }
    }

    internal static byte[] EncodeAccepted(ProjectLifecycleAccepted accepted) {
        ArgumentNullException.ThrowIfNull(accepted);
        return Encode(ProjectLifecycleAccepted.MaximumPayloadBytes, writer => {
            writer.WriteStartObject();
            writer.WriteString("domain", ResultDomain);
            writer.WriteNumber("resultVersion", accepted.ResultVersion);
            WriteSubmission(writer, accepted.Original);
            writer.WriteString("acceptedAtUtc", accepted.AcceptedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("replayUntilUtc", accepted.ReplayUntilUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartObject("acknowledgement");
            switch (accepted.Acknowledgement) {
                case ProjectLifecycleAcknowledgement.DeletionCommitted deletion:
                    writer.WritePropertyName("completion");
                    writer.WriteRawValue(ProjectDeletionCompletionCodec.Write(deletion.Completion));
                    break;
                case ProjectLifecycleAcknowledgement.LinkEnsured ensured:
                    writer.WriteString("edge", ensured.EdgeId);
                    writer.WriteBoolean("created", ensured.Created);
                    break;
                case ProjectLifecycleAcknowledgement.LinkRemoved:
                    break;
                case ProjectLifecycleAcknowledgement.LinkReconnected reconnected:
                    writer.WriteString("edge", reconnected.DestinationEdgeId);
                    writer.WriteBoolean("created", reconnected.Created);
                    break;
                default:
                    throw new NotSupportedException("The lifecycle acknowledgement is unsupported.");
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    internal static ProjectLifecycleAccepted DecodeAccepted(ReadOnlySpan<byte> bytes, ProjectLifecycleOperation expected) {
        ArgumentNullException.ThrowIfNull(expected);
        if (bytes.Length is 0 or > ProjectLifecycleAccepted.MaximumPayloadBytes) {
            throw new InvalidDataException("The lifecycle receipt is outside its supported byte bound.");
        }
        try {
            using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.GetProperty("domain").GetString() != ResultDomain) {
                throw new InvalidDataException("The lifecycle receipt has an unsupported domain.");
            }
            var original = ReadSubmission(root);
            if (original.Operation != expected) {
                throw new InvalidDataException("The retained receipt belongs to another original lifecycle operation.");
            }
            var result = root.GetProperty("acknowledgement");
            ProjectLifecycleAcknowledgement acknowledgement = original.Command switch {
                ProjectLifecycleCommand.Delete deletion => new ProjectLifecycleAcknowledgement.DeletionCommitted(
                    ProjectDeletionCompletionCodec.Read(Encoding.UTF8.GetBytes(result.GetProperty("completion").GetRawText()),
                        ReadDeletionOriginal(result.GetProperty("completion"), original, deletion))),
                ProjectLifecycleCommand.EnsureLink => new ProjectLifecycleAcknowledgement.LinkEnsured(
                    result.GetProperty("edge").GetGuid(), result.GetProperty("created").GetBoolean()),
                ProjectLifecycleCommand.RemoveLink => new ProjectLifecycleAcknowledgement.LinkRemoved(),
                ProjectLifecycleCommand.ReconnectLink => new ProjectLifecycleAcknowledgement.LinkReconnected(
                    result.GetProperty("edge").GetGuid(), result.GetProperty("created").GetBoolean()),
                _ => throw new NotSupportedException("The lifecycle command is unsupported.")
            };
            var accepted = new ProjectLifecycleAccepted(original, root.GetProperty("resultVersion").GetInt32(),
                root.GetProperty("acceptedAtUtc").GetDateTimeOffset(), root.GetProperty("replayUntilUtc").GetDateTimeOffset(),
                acknowledgement);
            if (!bytes.SequenceEqual(EncodeAccepted(accepted))) {
                throw new InvalidDataException("The lifecycle receipt is not the exact supported canonical encoding.");
            }
            return accepted;
        } catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or FormatException or KeyNotFoundException) {
            throw new InvalidDataException("The retained lifecycle receipt is invalid.", failure);
        }
    }

    private static ProjectDeletionPreparationContext ReadDeletionOriginal(JsonElement completion,
        ProjectLifecycleSubmission original, ProjectLifecycleCommand.Delete deletion)
        => new(new(original.Operation.DatabaseProfileId, original.Operation.Namespace,
            new(completion.GetProperty("completion").GetGuid())), deletion.Expected.Project);

    private static byte[] Encode(int maximum, Action<Utf8JsonWriter> write) {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            write(writer);
        }
        if (stream.Length > maximum) {
            throw new InvalidDataException("The canonical lifecycle value exceeds its supported byte bound.");
        }
        return stream.ToArray();
    }

    private static void WriteSubmission(Utf8JsonWriter writer, ProjectLifecycleSubmission original) {
        RequireVersion(original.Interpretation);
        writer.WriteNumber("schema", original.Interpretation.Schema);
        writer.WriteNumber("normalization", original.Interpretation.Normalization);
        writer.WriteStartObject("operation");
        writer.WriteString("profile", original.Operation.DatabaseProfileId);
        writer.WriteString("namespace", original.Operation.Namespace.Value);
        writer.WriteNumber("epoch", original.Operation.Epoch.Value);
        writer.WriteString("caller", original.Operation.CallerBinding.Value);
        writer.WriteString("id", original.Operation.Id);
        writer.WriteEndObject();
        writer.WriteNumber("kind", (int)original.Command.Kind);
        writer.WriteStartObject("command");
        switch (original.Command) {
            case ProjectLifecycleCommand.Delete deletion:
                WriteProject(writer, "project", deletion.Expected.Project);
                writer.WriteString("revision", deletion.Expected.Revision);
                break;
            case ProjectLifecycleCommand.EnsureLink ensure:
                WriteProject(writer, "parent", ensure.Parent);
                WriteProject(writer, "child", ensure.Child);
                break;
            case ProjectLifecycleCommand.RemoveLink remove:
                WriteEdge(writer, remove.Expected);
                break;
            case ProjectLifecycleCommand.ReconnectLink reconnect:
                WriteEdge(writer, reconnect.Expected);
                WriteProject(writer, "newParent", reconnect.NewParent);
                break;
            default:
                throw new NotSupportedException("The lifecycle command is unsupported.");
        }
        writer.WriteEndObject();
    }

    private static ProjectLifecycleSubmission ReadSubmission(JsonElement root) {
        var operation = root.GetProperty("operation");
        var original = new ProjectLifecycleOperation(operation.GetProperty("profile").GetGuid(),
            new(operation.GetProperty("namespace").GetGuid()), new(operation.GetProperty("epoch").GetInt64()),
            new(operation.GetProperty("caller").GetString()!), operation.GetProperty("id").GetGuid());
        var interpretation = new ProjectLifecycleInterpretation(root.GetProperty("schema").GetInt32(), root.GetProperty("normalization").GetInt32());
        RequireVersion(interpretation);
        var value = root.GetProperty("command");
        ProjectLifecycleCommand command = (ProjectLifecycleCommandKind)root.GetProperty("kind").GetInt32() switch {
            ProjectLifecycleCommandKind.Delete => new ProjectLifecycleCommand.Delete(new(ReadProject(value.GetProperty("project")), value.GetProperty("revision").GetGuid())),
            ProjectLifecycleCommandKind.EnsureLink => new ProjectLifecycleCommand.EnsureLink(ReadProject(value.GetProperty("parent")), ReadProject(value.GetProperty("child"))),
            ProjectLifecycleCommandKind.RemoveLink => new ProjectLifecycleCommand.RemoveLink(ReadEdge(value)),
            ProjectLifecycleCommandKind.ReconnectLink => new ProjectLifecycleCommand.ReconnectLink(ReadEdge(value), ReadProject(value.GetProperty("newParent"))),
            _ => throw new NotSupportedException("The retained lifecycle command is unsupported.")
        };
        return new(original, interpretation, command);
    }

    private static void WriteEdge(Utf8JsonWriter writer, ProjectHierarchyEdgeExpectation edge) {
        writer.WriteString("edge", edge.EdgeId);
        WriteProject(writer, "parent", edge.Parent);
        WriteProject(writer, "child", edge.Child);
    }

    private static ProjectHierarchyEdgeExpectation ReadEdge(JsonElement value)
        => new(value.GetProperty("edge").GetGuid(), ReadProject(value.GetProperty("parent")), ReadProject(value.GetProperty("child")));

    private static void WriteProject(Utf8JsonWriter writer, string name, ProjectWriteAdmission project) {
        writer.WriteStartObject(name);
        writer.WriteString("profile", project.DatabaseProfileId);
        writer.WriteString("id", project.ProjectId);
        writer.WriteString("lifetime", project.LifetimeId);
        writer.WriteEndObject();
    }

    private static ProjectWriteAdmission ReadProject(JsonElement value)
        => new(value.GetProperty("profile").GetGuid(), value.GetProperty("id").GetGuid(), value.GetProperty("lifetime").GetGuid());

    private static void RequireVersion(ProjectLifecycleInterpretation interpretation) {
        if (interpretation != ProjectLifecycleInterpretation.VersionOne) {
            throw new NotSupportedException("The lifecycle intent interpretation is unsupported.");
        }
    }
}
