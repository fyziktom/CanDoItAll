using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionRecoveryCodec : IProjectDefinitionRecoveryCodec {
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public ProjectDefinitionFrozenSubmission Freeze(ProjectDefinitionSubmission submission, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(submission);
        RequireVersion(submission);
        ValidateInput(submission, limits);
        using var stream = new BoundedStream(limits.MaxRawRequestBytes);
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            writer.WriteNumber("kind", (int)submission.Kind);
            writer.WritePropertyName("request");
            switch (submission) {
                case ProjectDefinitionSubmission.Create create:
                    JsonSerializer.Serialize(writer, create.Request, Json);
                    break;
                case ProjectDefinitionSubmission.Replace replace:
                    JsonSerializer.Serialize(writer, replace.Request, Json);
                    break;
                case ProjectDefinitionSubmission.Scalars scalars:
                    JsonSerializer.Serialize(writer, scalars.Request, Json);
                    break;
            }
            writer.WriteEndObject();
        }
        return new(submission, Utf8.GetString(stream.ToArray()));
    }

    public ProjectDefinitionFrozenSubmission ReadSubmission(string payload, ProjectDefinitionLimits limits) {
        RequireBytes(payload, limits.MaxRawRequestBytes);
        using var document = JsonDocument.Parse(payload, new() { MaxDepth = Json.MaxDepth });
        var root = document.RootElement;
        if (root.EnumerateObject().Count() != 2) {
            throw new JsonException("The pending submission envelope is unsupported.");
        }
        var request = root.GetProperty("request");
        var command = request.GetProperty("command");
        var kind = (ProjectDefinitionCommandKind)root.GetProperty("kind").GetInt32();
        if (kind is ProjectDefinitionCommandKind.Create or ProjectDefinitionCommandKind.Replace) {
            RequireCount(command.GetProperty("phases"), limits.MaxPhaseCount);
            RequireCount(command.GetProperty("options"), limits.MaxOptionCount);
        }
        ProjectDefinitionSubmission submission = kind switch {
            ProjectDefinitionCommandKind.Create => new ProjectDefinitionSubmission.Create(
                request.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(Json)!),
            ProjectDefinitionCommandKind.Replace => new ProjectDefinitionSubmission.Replace(
                request.Deserialize<ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>>(Json)!),
            ProjectDefinitionCommandKind.UpdateScalars => new ProjectDefinitionSubmission.Scalars(
                request.Deserialize<ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand>>(Json)!),
            _ => throw new JsonException("The pending submission command kind is unsupported.")
        };
        var frozen = Freeze(submission, limits);
        if (!StringComparer.Ordinal.Equals(payload, frozen.Payload)) {
            throw new JsonException("The pending submission is not in its supported frozen representation.");
        }
        return frozen;
    }

    public ProjectDefinitionReceiptPayload EncodeAccepted(ProjectDefinitionSubmission submission,
        ProjectDefinitionAcceptedReceipt receipt, ProjectDefinitionLimits limits) {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(receipt);
        RequireVersion(submission);
        ValidateInput(submission, limits);
        if (receipt.Operation != submission.Operation || receipt.Kind != submission.Kind) {
            throw new ArgumentException("The accepted receipt belongs to a different submitted operation or command kind.", nameof(receipt));
        }
        ProjectDefinitionEncodedResult encoded;
        if (receipt is ProjectDefinitionAcceptedReceipt.Full full) {
            var (expected, phases, options) = submission switch {
                ProjectDefinitionSubmission.Create create => ((ProjectDefinitionExpectation?)null, create.Request.Command.Phases, create.Request.Command.Options),
                ProjectDefinitionSubmission.Replace replace => (replace.Request.Command.Expected, replace.Request.Command.Phases, replace.Request.Command.Options),
                _ => throw new ArgumentException("A scalar submission cannot accept a full receipt.", nameof(receipt))
            };
            ValidateExpectation(expected, full.Result.Acknowledgement.Definition);
            ValidateRows(full.Result.Acknowledgement.Phases, phases.Count, static row => (row.SubmittedIndex, row.Id),
                index => phases[index].Id, static _ => false);
            ValidateRows(full.Result.Acknowledgement.Options, options.Count, static row => (row.SubmittedIndex, row.Id),
                index => options[index].Id, index => string.IsNullOrWhiteSpace(options[index].OptionName) && string.IsNullOrWhiteSpace(options[index].Notes));
            encoded = ProjectDefinitionOperationCodec.EncodeAccepted(full.Result, limits);
        } else if (receipt is ProjectDefinitionAcceptedReceipt.Scalars scalars && submission is ProjectDefinitionSubmission.Scalars scalarSubmission) {
            ValidateExpectation(scalarSubmission.Request.Command.Expected, scalars.Result.Acknowledgement.Definition);
            encoded = ProjectDefinitionOperationCodec.EncodeAccepted(scalars.Result, limits);
        } else {
            throw new ArgumentException("The accepted receipt shape does not match the submission.", nameof(receipt));
        }
        return new(receipt, Utf8.GetString(encoded.ToArray()));
    }

    public ProjectDefinitionReceiptPayload ReadAccepted(ProjectDefinitionSubmission submission, string payload, ProjectDefinitionLimits limits) {
        RequireBytes(payload, limits.MaxAcceptedResultBytes);
        var decoded = ProjectDefinitionOperationCodec.DecodeAccepted(ProjectDefinitionResultVersion.VersionOne, Utf8.GetBytes(payload), limits);
        ProjectDefinitionAcceptedReceipt receipt = decoded switch {
            ProjectDefinitionDecodedReceipt.Full full => new ProjectDefinitionAcceptedReceipt.Full(full.Result),
            ProjectDefinitionDecodedReceipt.Scalars scalars => new ProjectDefinitionAcceptedReceipt.Scalars(scalars.Result),
            _ => throw new JsonException("The accepted receipt shape is unsupported.")
        };
        return EncodeAccepted(submission, receipt, limits);
    }

    private static void RequireVersion(ProjectDefinitionSubmission submission) {
        if (submission.Version != ProjectDefinitionRequestVersion.VersionOne) {
            throw new NotSupportedException("The pending submission interpretation is unsupported.");
        }
    }

    private static void ValidateInput(ProjectDefinitionSubmission submission, ProjectDefinitionLimits limits) {
        switch (submission) {
            case ProjectDefinitionSubmission.Create create:
                ProjectDefinitionInputBounds.Validate(create.Request.Command.Scalars, create.Request.Command.Phases,
                    create.Request.Command.Options, create.Request.Command.ExternalIdentity, limits);
                break;
            case ProjectDefinitionSubmission.Replace replace:
                ProjectDefinitionInputBounds.Validate(replace.Request.Command.Scalars, replace.Request.Command.Phases,
                    replace.Request.Command.Options, replace.Request.Command.AssignExternalIdentity, limits);
                break;
            case ProjectDefinitionSubmission.Scalars scalars:
                ProjectDefinitionInputBounds.Validate(scalars.Request.Command.Scalars, null, null, null, limits);
                break;
        }
    }

    private static void ValidateExpectation(ProjectDefinitionExpectation? expected, ProjectDefinitionExpectation accepted) {
        ArgumentNullException.ThrowIfNull(accepted);
        if (expected is not null && (accepted.Project != expected.Project || accepted.Revision == expected.Revision)) {
            throw new ArgumentException("The accepted receipt does not advance the original project definition.");
        }
    }

    private static void ValidateRows<T>(IReadOnlyList<T> rows, int count, Func<T, (int Index, Guid Id)> identify,
        Func<int, Guid?> originalId, Func<int, bool> mayOmit) where T : class {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > count) {
            throw new ArgumentException("The accepted receipt contains extra submitted slots.");
        }
        var indices = new HashSet<int>();
        var ids = new HashSet<Guid>();
        foreach (var row in rows) {
            ArgumentNullException.ThrowIfNull(row);
            var (index, id) = identify(row);
            if (index < 0 || index >= count || id == Guid.Empty || !indices.Add(index) || !ids.Add(id) ||
                originalId(index) is { } original && original != id) {
                throw new ArgumentException("The accepted receipt has an invalid submitted slot or child identity.");
            }
        }
        for (var index = 0; index < count; index++) {
            if (!indices.Contains(index) && !mayOmit(index)) {
                throw new ArgumentException("The accepted receipt omits a submitted row.");
            }
        }
    }

    private static void RequireCount(JsonElement value, int maximum) {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum) {
            throw new JsonException("The pending submission exceeds its child count limit.");
        }
    }

    private static void RequireBytes(string payload, int maximum) {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        if (payload.Length > maximum || Utf8.GetByteCount(payload) > maximum) {
            throw new ArgumentException("The pending payload exceeds its supported UTF-8 byte limit.", nameof(payload));
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
                throw new ArgumentException("The frozen submission exceeds its supported byte limit.");
            }
            var required = (int)Length + count;
            if (required > Capacity) {
                Capacity = (int)Math.Min(maximumBytes, Math.Max(required, Math.Max(256L, 2L * Capacity)));
            }
        }
    }
}
