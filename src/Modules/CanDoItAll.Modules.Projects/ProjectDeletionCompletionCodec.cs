using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectDeletionCompletionCodec {
    internal const int MaximumEncodedBytes = 16_384;
    private const string Domain = "projects:deletion-completion:v1";
    private const int Version = 1;

    internal static byte[] Write(ProjectDeletionCompletionBinding binding) {
        ArgumentNullException.ThrowIfNull(binding);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) {
            writer.WriteStartObject();
            writer.WriteString("domain", Domain);
            writer.WriteNumber("version", Version);
            var original = binding.Original;
            writer.WriteString("profile", original.Completion.DatabaseProfileId);
            writer.WriteString("namespace", original.Completion.Namespace.Value);
            writer.WriteString("completion", original.Completion.Id.Value);
            writer.WriteString("project", original.Project.ProjectId);
            writer.WriteString("lifetime", original.Project.LifetimeId);
            writer.WriteStartArray("obligations");
            foreach (var obligation in binding.Obligations) {
                writer.WriteStartObject();
                writer.WriteString("participant", obligation.Participant.Value);
                writer.WriteString("recovery", obligation.RecoveryId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        if (output.Length > MaximumEncodedBytes) {
            throw new InvalidDataException("Deletion completion exceeds its encoded byte bound.");
        }
        return output.ToArray();
    }

    internal static ProjectDeletionCompletionBinding Read(ReadOnlySpan<byte> encoded,
        ProjectDeletionPreparationContext expected) {
        ArgumentNullException.ThrowIfNull(expected);
        if (encoded.IsEmpty || encoded.Length > MaximumEncodedBytes) {
            throw new InvalidDataException("Deletion completion is empty or exceeds its encoded byte bound.");
        }
        try {
            using var document = JsonDocument.Parse(encoded.ToArray(), new() { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.GetProperty("domain").GetString() != Domain || root.GetProperty("version").GetInt32() != Version) {
                throw new InvalidDataException("The deletion completion interpretation is unsupported.");
            }
            var profile = root.GetProperty("profile").GetGuid();
            var original = new ProjectDeletionPreparationContext(
                new(profile, new DatasetNamespaceId(root.GetProperty("namespace").GetGuid()),
                    new(root.GetProperty("completion").GetGuid())),
                new(profile, root.GetProperty("project").GetGuid(), root.GetProperty("lifetime").GetGuid()));
            if (original != expected) {
                throw new InvalidDataException("Deletion completion does not match its original scope and project lifetime.");
            }
            var array = root.GetProperty("obligations");
            var count = array.GetArrayLength();
            if (count > ProjectDeletionCompletionBinding.MaximumParticipants) {
                throw new InvalidDataException("Deletion completion exceeds its participant bound.");
            }
            var obligations = new ProjectDeletionObligation[count];
            var index = 0;
            foreach (var item in array.EnumerateArray()) {
                obligations[index++] = new(new ProjectDeletionParticipantId(item.GetProperty("participant").GetString()!),
                    item.GetProperty("recovery").GetGuid());
            }
            var binding = new ProjectDeletionCompletionBinding(original, obligations);
            if (!encoded.SequenceEqual(Write(binding))) {
                throw new InvalidDataException("Deletion completion is not its exact canonical representation.");
            }
            return binding;
        } catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or
                FormatException or KeyNotFoundException) {
            throw new InvalidDataException("The retained deletion completion is invalid.", failure);
        }
    }
}
