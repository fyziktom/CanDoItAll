using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanOperationCodec {
    internal const int CurrentVersion = 1;
    private const int MaximumCommandBytes = 3 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 24
    };

    internal static string Digest(ProjectTaskPlanCommand command) {
        ArgumentNullException.ThrowIfNull(command);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var destination = new DigestStream(hash);
        using (var writer = new Utf8JsonWriter(destination)) {
            writer.WriteStartObject();
            writer.WriteString("domain", "workbench:task-plan:command:v1");
            writer.WriteString("pool", WorkbenchTaskPlanFiniteOperations.Pool.Value);
            writer.WriteString("family", WorkbenchTaskPlanFiniteOperations.Family.Value);
            writer.WriteNumber("schemaVersion", command.SchemaVersion);
            writer.WriteNumber("normalizationVersion", command.NormalizationVersion);
            writer.WritePropertyName("operation");
            JsonSerializer.Serialize(writer, Address(command.Operation), Options);
            writer.WritePropertyName("basis");
            writer.WriteRawValue(ProjectTaskPlanBasisCodec.Encode(command.Basis));
            writer.WritePropertyName("tasks");
            JsonSerializer.Serialize(writer, command.Proposal.Tasks, Options);
            writer.WritePropertyName("policy");
            JsonSerializer.Serialize(writer, command.Policy, Options);
            writer.WritePropertyName("source");
            JsonSerializer.Serialize(writer, command.Source, Options);
            writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static byte[] EncodeAccepted(ProjectTaskPlanAccepted accepted) {
        ArgumentNullException.ThrowIfNull(accepted);
        using var stream = new BoundedStream();
        JsonSerializer.Serialize(stream, new StoredAcceptance(CurrentVersion, Address(accepted.Operation),
            accepted.OriginalDefinition, accepted.Source, accepted.SemanticDigest, accepted.AcceptedAtUtc,
            accepted.ReplayUntilUtc, accepted.Tasks), Options);
        return stream.ToArray();
    }

    internal static ProjectTaskPlanAccepted DecodeAccepted(ReadOnlyMemory<byte> payload) {
        try {
            ProjectTaskPlanBasisLimits.RequireBytes(payload.Length, ProjectTaskPlanReceiptRecord.MaximumPayloadBytes);
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 24 });
            RequireUniqueMembers(document.RootElement);
            if (!document.RootElement.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array ||
                    tasks.GetArrayLength() is < 1 or > ProjectTaskPlanProposal.MaximumTasks) {
                throw new JsonException();
            }
            var stored = document.Deserialize<StoredAcceptance>(Options) ?? throw new JsonException();
            if (stored.Version != CurrentVersion) {
                throw new JsonException();
            }
            var operation = new ProjectTaskPlanOperation(new(stored.Operation.Namespace), stored.Operation.Epoch,
                stored.Operation.Id, stored.Operation.CallerBinding);
            var accepted = new ProjectTaskPlanAccepted(operation, stored.OriginalDefinition, stored.Source,
                stored.SemanticDigest, stored.AcceptedAtUtc, stored.ReplayUntilUtc, stored.Tasks);
            foreach (var task in accepted.Tasks) {
                RequireTaskCorrespondence(accepted.OriginalDefinition.Project.ProjectId, task);
            }
            return accepted;
        } catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidOperationException) {
            throw new ProjectTaskPlanReceiptDecodeException();
        }
    }

    private static void RequireTaskCorrespondence(Guid projectId, ProjectTaskPlanStagedTask task) {
        var fact = task.Fact;
        using var metadata = JsonDocument.Parse(task.MetadataJson, new JsonDocumentOptions { MaxDepth = 24 });
        RequireUniqueMembers(metadata.RootElement);
        var projected = ProjectTaskPlanMaterialProjection.Project(new() {
            Id = fact.RecordId, NodeKey = fact.NodeKey, ParentNodeKey = fact.ParentNodeKey,
            ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            Title = fact.Title, Subtitle = fact.Subtitle, Notes = fact.Notes, Status = fact.Status,
            Priority = fact.Priority, ProgressMode = fact.ProgressMode, ProgressPercent = fact.ProgressPercent,
            StartUtc = fact.StartUtc, EndUtc = fact.EndUtc, DurationSeconds = fact.DurationSeconds, MetadataJson = task.MetadataJson
        });
        if (projected != fact || fact.ParentNodeKey != ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId) ||
                fact.NodeKey != $"{ProjectWorkbenchGraphConventions.CustomNodePrefix}{fact.RecordId:N}" ||
                fact.StartUtc is not null || fact.EndUtc is not null || fact.DurationSeconds is not null || fact.DueUtc is not null ||
                fact.Status != "Draft" || fact.Subtitle != "Task" || fact.Execution.State != ProjectTaskExecutionState.NotStarted ||
                fact.Priority is < 0 or > 6 || fact.Title.Length is < 1 or > ProjectTaskPlanProposal.MaximumTitleCharacters ||
                fact.Title != fact.Title.Trim() || fact.Description != fact.Description.Trim() || fact.Notes != fact.Notes.Trim()) {
            throw new JsonException();
        }
        _ = ProjectNodeBindingStorage.PrepareNewPlainTaskBinding(new() {
            Id = fact.RecordId, ProjectId = projectId, NodeKey = fact.NodeKey, ParentNodeKey = fact.ParentNodeKey,
            ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            Title = fact.Title, Subtitle = fact.Subtitle, Notes = fact.Notes, Status = fact.Status, Priority = fact.Priority,
            ProgressMode = fact.ProgressMode, ProgressPercent = fact.ProgressPercent, MetadataJson = task.MetadataJson,
            PositionX = task.PositionX, PositionY = task.PositionY, CreatedAtUtc = task.CreatedAtUtc, UpdatedAtUtc = task.CreatedAtUtc,
            NodeReferences = new()
        });
    }

    private static void RequireUniqueMembers(JsonElement value) {
        if (value.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException();
                }
                if (property.Name.EndsWith("Utc", StringComparison.Ordinal) && property.Value.ValueKind != JsonValueKind.Null &&
                        (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } text ||
                            !(text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal)))) {
                    throw new JsonException();
                }
                RequireUniqueMembers(property.Value);
            }
        } else if (value.ValueKind == JsonValueKind.Array) {
            foreach (var item in value.EnumerateArray()) {
                RequireUniqueMembers(item);
            }
        }
    }

    private static StoredAddress Address(ProjectTaskPlanOperation operation)
        => new(operation.Namespace.Value, operation.Epoch, operation.Id, operation.CallerBinding);
    private sealed record StoredAddress(Guid Namespace, long Epoch, Guid Id, string CallerBinding);
    private sealed record StoredAcceptance(int Version, StoredAddress Operation, ProjectDefinitionExpectation OriginalDefinition,
        ProjectTaskPlanSource Source, string SemanticDigest, DateTimeOffset AcceptedAtUtc, DateTimeOffset ReplayUntilUtc,
        IReadOnlyList<ProjectTaskPlanStagedTask> Tasks);

    private sealed class BoundedStream : MemoryStream {
        public override void Write(byte[] buffer, int offset, int count) {
            PrepareWrite(count);
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer) {
            PrepareWrite(buffer.Length);
            base.Write(buffer);
        }
        private void PrepareWrite(int count) {
            ProjectTaskPlanBasisLimits.RequireBytes(Length + count, ProjectTaskPlanReceiptRecord.MaximumPayloadBytes);
            var required = checked((int)Length + count);
            if (required > Capacity) {
                Capacity = (int)Math.Min(ProjectTaskPlanReceiptRecord.MaximumPayloadBytes, Math.Max(required, Math.Max(256L, 2L * Capacity)));
            }
        }
    }

    private sealed class DigestStream(IncrementalHash hash) : Stream {
        private long length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position {
            get => length;
            set => throw new NotSupportedException();
        }
        public override void Flush() {
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) {
            ProjectTaskPlanBasisLimits.RequireBytes(length + buffer.Length, MaximumCommandBytes);
            hash.AppendData(buffer);
            length += buffer.Length;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

internal sealed class ProjectTaskPlanReceiptDecodeException() : InvalidOperationException("The stored task-plan acceptance is invalid.");
