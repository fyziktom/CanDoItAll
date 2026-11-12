using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class DatasetActivationRecordCodec {
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static DatasetActivationFileRecord Decode(ReadOnlySpan<byte> bytes, Guid profileId) {
        if (bytes.Length > DatasetActivationService.MaximumRecordBytes) {
            throw new InvalidDataException("The dataset activation file exceeds its bounded record size.");
        }
        DatasetActivationFileRecord? record;
        try {
            record = JsonSerializer.Deserialize<DatasetActivationFileRecord>(bytes, Options);
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            throw new InvalidDataException("The dataset activation file is invalid or unsupported; explicit recovery is required.", exception);
        }
        if (record is null || record.ProfileId != profileId) {
            throw new InvalidDataException("The dataset activation file does not identify the requested profile.");
        }
        return record;
    }
}
