using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanBasisCodec {
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 16
    };

    internal static byte[] Encode(ProjectTaskPlanBasis basis) => Encode(basis, basis.CapturedAtUtc);

    internal static ProjectTaskPlanBasis Decode(ReadOnlyMemory<byte> payload) {
        ProjectTaskPlanBasisLimits.RequireBytes(payload.Length, ProjectTaskPlanBasisLimits.MaximumEncodedBytes);
        try {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            RequireUniqueMembers(document.RootElement);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array) {
                throw new JsonException();
            }
            ProjectTaskPlanBasisLimits.RequireBytes(tasks.GetArrayLength(), ProjectTaskPlanBasisLimits.MaximumCandidates);
            var value = document.Deserialize<SerializedBasis>(Options) ?? throw new JsonException();
            if (value.Version != ProjectTaskPlanBasisLimits.Version ||
                    value.Descriptor != ProjectTaskPlanBasisDescriptor.CanonicalManualTaskPlanV1) {
                throw new JsonException();
            }
            var expected = new ProjectDefinitionExpectation(
                new(value.DatabaseProfileId, value.ProjectId, value.LifetimeId), value.DefinitionRevision);
            return new(new DatasetNamespaceId(value.Namespace), new(expected, value.Scalars), value.CapturedAtUtc, value.Tasks);
        } catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
    }

    internal static bool MaterialEquals(ProjectTaskPlanBasis left, ProjectTaskPlanBasis right)
        => left.Descriptor == right.Descriptor && left.Version == right.Version && left.Namespace == right.Namespace &&
            left.Project == right.Project && left.Tasks.SequenceEqual(right.Tasks);

    internal static string MaterialDigest(ProjectTaskPlanBasis basis)
        => Convert.ToHexStringLower(SHA256.HashData(Encode(basis, DateTimeOffset.UnixEpoch)));

    private static byte[] Encode(ProjectTaskPlanBasis basis, DateTimeOffset capturedAtUtc) {
        ArgumentNullException.ThrowIfNull(basis);
        var expected = basis.Project.Expectation;
        var value = new SerializedBasis(basis.Version, basis.Descriptor, basis.Namespace.Value,
            expected.Project.DatabaseProfileId, expected.Project.ProjectId, expected.Project.LifetimeId,
            expected.Revision, basis.Project.Scalars, capturedAtUtc, basis.Tasks);
        using var stream = new BoundedStream();
        JsonSerializer.Serialize(stream, value, Options);
        return stream.ToArray();
    }

    internal static void RequireUniqueMembers(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException();
                }
                if (property.Name.EndsWith("Utc", StringComparison.Ordinal) && property.Value.ValueKind != JsonValueKind.Null &&
                        (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } instant ||
                            !(instant.EndsWith('Z') || instant.EndsWith("+00:00", StringComparison.Ordinal)))) {
                    throw new JsonException();
                }
                RequireUniqueMembers(property.Value);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                RequireUniqueMembers(item);
            }
        }
    }

    private sealed record SerializedBasis(int Version, ProjectTaskPlanBasisDescriptor Descriptor, Guid Namespace,
        Guid DatabaseProfileId, Guid ProjectId, Guid LifetimeId, Guid DefinitionRevision,
        ProjectDefinitionScalars Scalars, DateTimeOffset CapturedAtUtc, IReadOnlyList<ProjectTaskPlanTaskFact> Tasks);

    private sealed class BoundedStream() : MemoryStream {
        public override void Write(byte[] buffer, int offset, int count) {
            RequireCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            RequireCapacity(buffer.Length);
            base.Write(buffer);
        }

        private void RequireCapacity(int count) {
            ProjectTaskPlanBasisLimits.RequireBytes(Length + count, ProjectTaskPlanBasisLimits.MaximumEncodedBytes);
            var required = (int)Length + count;
            if (required > Capacity) {
                Capacity = (int)Math.Min(ProjectTaskPlanBasisLimits.MaximumEncodedBytes,
                    Math.Max(required, Math.Max(256L, 2L * Capacity)));
            }
        }
    }
}
